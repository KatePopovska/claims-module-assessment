using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaimsModule.Application.Claims.Commands.CreateClaim;
using ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Application.Reserves.Queries.GetClaimReserves;

namespace ClaimsModule.IntegrationTests.Infrastructure;

public static class ApiTestHelpers
{
    public static readonly Guid MeridianPolicyId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly DateTimeOffset LossDateWithinMeridianPolicy = new(2025, 6, 15, 10, 0, 0, TimeSpan.Zero);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static object ClaimRequest(Guid? policyId = null, bool withRiskObject = false, object? initialReserve = null) => new
    {
        policyId,
        lossDate = LossDateWithinMeridianPolicy,
        lossDescription = "Fire in the loading bay destroyed two pallets of goods.",
        lossLocation = "Warehouse 4",
        causeOfLossCode = "COL-FIRE",
        estimatedLossAmount = 12_500m,
        policeReportNumber = (string?)null,
        parties = new[] { new { partyRole = "Claimant", partyType = "Person", firstName = "Ann", lastName = "Lee", companyName = (string?)null, email = (string?)null, phone = (string?)null } },
        riskObjects = withRiskObject
            ? new object[] { new { assetType = "Property", assetDescription = "Loading bay stock", damageDescription = (string?)null, isPrimary = true, assetReference = (string?)null } }
            : [],
        initialReserve,
    };

    public static async Task<ClaimCreatedDto> CreateClaimAsync(HttpClient client, object request)
    {
        var response = await client.PostAsJsonAsync("/api/claims", request, Json);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<ClaimCreatedDto>(Json))!;
    }

    public static async Task<PagedResult<AuditLogEntryDto>> GetAuditLogAsync(HttpClient client, Guid claimId) =>
        (await client.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/claims/{claimId}/audit?page=1&pageSize=100", Json))!;

    public static async Task<ClaimReservesDto> GetReservesAsync(HttpClient client, Guid claimId) =>
        (await client.GetFromJsonAsync<ClaimReservesDto>($"/api/claims/{claimId}/reserves", Json))!;

    public static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode} {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    public static async Task<T> EventuallyAsync<T>(Func<Task<T>> probe, Func<T, bool> isDone, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            var value = await probe();
            if (isDone(value) || DateTimeOffset.UtcNow > deadline)
            {
                return value;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }
    }
}
