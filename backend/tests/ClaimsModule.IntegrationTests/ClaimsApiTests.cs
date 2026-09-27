using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ClaimsModule.Application.Claims.Commands.CreateClaim;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Application.Reference.Queries.GetCauseOfLossCodes;
using ClaimsModule.Application.Reference.Queries.SearchPolicies;
using ClaimsModule.Application.Claims.Queries.ListClaims;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using ClaimsModule.IntegrationTests.Infrastructure;
using static ClaimsModule.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace ClaimsModule.IntegrationTests;

[Collection(IntegrationCollection.Name)]
[Trait("Category", "Integration")]
public class ClaimsApiTests(SqlServerApiFixture fixture)
{
    private static readonly TimeSpan JobTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Migrations_CreateSchemaWithReferenceData()
    {
        var client = fixture.CreateHandlerClient();

        var causes = await client.GetFromJsonAsync<List<CauseOfLossCodeDto>>("/api/reference/cause-of-loss-codes", Json);
        var policies = await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?q=POL", Json);

        Assert.Equal(10, causes!.Count);
        Assert.Equal(5, policies!.Count);
    }

    [Fact]
    public async Task CreateClaim_RecordsClaimCreatedAndIntakeWarningsInAuditLog()
    {
        var client = fixture.CreateHandlerClient();

        var created = await CreateClaimAsync(client, ClaimRequest(policyId: null));
        var audit = await GetAuditLogAsync(client, created.Id);

        Assert.Matches(new Regex(@"^CLM-\d{4}-\d{7}$"), created.ClaimNumber);
        Assert.Equal([ClaimIntakeRules.PolicyUnknownWarning, ClaimIntakeRules.NoRiskObjectsWarning], created.Warnings);
        Assert.Single(audit.Items, e => e.EventType == AuditEventType.CLAIM_CREATED);
        Assert.Equal(
            created.Warnings.Order(),
            audit.Items.Where(e => e.EventType == AuditEventType.VALIDATION_ISSUE_ADDED).Select(e => e.Description).Order());
    }

    [Fact]
    public async Task CreateClaim_ClaimNumbersComeFromTheDatabaseSequence()
    {
        var client = fixture.CreateHandlerClient();

        var first = await CreateClaimAsync(client, ClaimRequest(MeridianPolicyId, withRiskObject: true));
        var second = await CreateClaimAsync(client, ClaimRequest(MeridianPolicyId, withRiskObject: true));

        Assert.Equal(SequenceNumber(first.ClaimNumber) + 1, SequenceNumber(second.ClaimNumber));
        Assert.Empty(first.Warnings);
    }

    [Fact]
    public async Task CreateClaim_WithInitialReserve_LinksReserveToDatabaseGeneratedClaimIdAndPostsToGl()
    {
        var client = fixture.CreateHandlerClient();

        var created = await CreateClaimAsync(client, ClaimRequest(MeridianPolicyId, withRiskObject: true, initialReserve: new { component = "Indemnity", amount = 5_000m, changeReason = "Initial estimate" }));
        var reserves = await EventuallyAsync(() => GetReservesAsync(client, created.Id), r => r.Transactions.Single().PostingStatus == ReservePostingStatus.Posted, JobTimeout);

        Assert.Equal(ReserveApprovalStatus.AutoApproved, created.InitialReserve!.Transaction.ApprovalStatus);
        Assert.Equal(5_000m, reserves.TotalReserves);
        Assert.Equal(ReservePostingStatus.Posted, reserves.Transactions.Single().PostingStatus);
        Assert.Single((await GetAuditLogAsync(client, created.Id)).Items, e => e.EventType == AuditEventType.GL_POSTING_SIMULATED);
    }

    [Fact]
    public async Task ApprovedReserve_IsPostedToTheGeneralLedgerExactlyOnce()
    {
        var handler = fixture.CreateHandlerClient();
        var supervisor = fixture.CreateSupervisorClient();
        var claim = await CreateClaimAsync(handler, ClaimRequest(MeridianPolicyId, withRiskObject: true));

        var submitted = await handler.PostAsJsonAsync($"/api/claims/{claim.Id}/reserves", new { component = "Indemnity", amount = 50_000m, changeReason = "Surveyor estimate" }, Json);
        await EnsureSuccessAsync(submitted);
        var pending = (await GetReservesAsync(handler, claim.Id)).Transactions.Single();
        Assert.Equal(ReserveApprovalStatus.PendingApproval, pending.ApprovalStatus);

        var approved = await supervisor.PostAsync($"/api/claims/{claim.Id}/reserves/{pending.Id}/approve", null);
        await EnsureSuccessAsync(approved);

        var reserves = await EventuallyAsync(() => GetReservesAsync(handler, claim.Id), r => r.Transactions.Single().PostingStatus == ReservePostingStatus.Posted, JobTimeout);
        var audit = await GetAuditLogAsync(handler, claim.Id);

        Assert.Equal(ReservePostingStatus.Posted, reserves.Transactions.Single().PostingStatus);
        Assert.Equal(50_000m, reserves.TotalReserves);
        Assert.Single(audit.Items, e => e.EventType == AuditEventType.RESERVE_APPROVED);
        Assert.Single(audit.Items, e => e.EventType == AuditEventType.GL_POSTING_SIMULATED);
    }

    [Fact]
    public async Task IdempotencyKey_ReplaysTheFirstResponseWithoutCreatingADuplicate()
    {
        var client = fixture.CreateHandlerClient();
        var key = Guid.NewGuid().ToString();
        var request = ClaimRequest(MeridianPolicyId, withRiskObject: true);

        var first = await PostWithKeyAsync(client, key, request);
        var second = await PostWithKeyAsync(client, key, request);
        var firstClaim = await first.Content.ReadFromJsonAsync<ClaimCreatedDto>(Json);
        var secondClaim = await second.Content.ReadFromJsonAsync<ClaimCreatedDto>(Json);
        var matching = await client.GetFromJsonAsync<PagedResult<ClaimSummaryDto>>($"/api/claims?search={firstClaim!.ClaimNumber}", Json);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal("true", second.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(firstClaim.Id, secondClaim!.Id);
        Assert.Equal(1, matching!.TotalCount);
    }

    [Fact]
    public async Task IdempotencyKey_ReusedForADifferentRequest_IsRejected()
    {
        var client = fixture.CreateHandlerClient();
        var key = Guid.NewGuid().ToString();

        var first = await PostWithKeyAsync(client, key, ClaimRequest(MeridianPolicyId, withRiskObject: true));
        var reused = await PostWithKeyAsync(client, key, ClaimRequest(policyId: null));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
    }

    [Fact]
    public async Task TenantFilter_HidesClaimsOfOtherOrganisations()
    {
        var otherOrganisation = Guid.NewGuid();
        var foreignClaim = new Claim
        {
            ClaimNumber = $"CLM-2026-9{Random.Shared.Next(100000, 999999)}",
            OrganisationId = otherOrganisation,
            Status = ClaimStatus.Draft,
            ReportedDate = DateTimeOffset.UtcNow,
        };
        await using (var context = fixture.CreateDbContext(otherOrganisation))
        {
            context.Claims.Add(foreignClaim);
            await context.SaveChangesAsync();
        }

        var client = fixture.CreateHandlerClient();
        var detail = await client.GetAsync($"/api/claims/{foreignClaim.Id}");
        var list = await client.GetFromJsonAsync<PagedResult<ClaimSummaryDto>>($"/api/claims?search={foreignClaim.ClaimNumber}", Json);

        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(0, list!.TotalCount);
    }

    private static async Task<HttpResponseMessage> PostWithKeyAsync(HttpClient client, string key, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/claims") { Content = JsonContent.Create(body, options: Json) };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    private static int SequenceNumber(string claimNumber) => int.Parse(claimNumber[^7..]);
}
