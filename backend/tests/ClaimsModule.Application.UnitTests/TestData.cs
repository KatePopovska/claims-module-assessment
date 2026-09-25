using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reserves;
using NSubstitute;

namespace ClaimsModule.Application.UnitTests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    public static readonly Guid HandlerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SupervisorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid ManagerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static Claim Claim(Guid? policyId = null) => new()
    {
        Id = Guid.NewGuid(),
        ClaimNumber = "CLM-2026-0000001",
        Status = ClaimStatus.Open,
        PolicyId = policyId ?? Guid.NewGuid(),
        Parties =
        [
            new ClaimParty { Id = Guid.NewGuid(), PartyRole = PartyRole.Claimant, PartyType = PartyType.Person, FirstName = "Ann", LastName = "Lee" }
        ]
    };

    public static ReserveHistory ApprovedTransaction(Claim claim, decimal amount = 5000)
    {
        var component = claim.OpenReserveComponent(ReserveComponentType.Indemnity);
        return component.RecordTransaction(amount, null, HandlerId, requiresApproval: false);
    }

    public static ReserveHistory PendingTransaction(Claim claim, decimal amount = 50000, Guid? submittedBy = null)
    {
        var component = claim.OpenReserveComponent(ReserveComponentType.Indemnity);
        return component.RecordTransaction(amount, null, submittedBy ?? HandlerId, requiresApproval: true);
    }

    public static ICurrentUserService User(string role, Guid userId)
    {
        var user = Substitute.For<ICurrentUserService>();
        user.Role.Returns(role);
        user.UserId.Returns(userId);
        return user;
    }

    public static IClaimRepository RepositoryReturning(Claim claim)
    {
        var repository = Substitute.For<IClaimRepository>();
        repository.GetWithReservesAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);
        repository.GetWithPartiesAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);
        repository.GetWithDocumentsAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);
        return repository;
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
