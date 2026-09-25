using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reference;
using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Domain.UnitTests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    public static readonly Guid HandlerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SupervisorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid ManagerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static Claim Claim(ClaimStatus status = ClaimStatus.Draft) => new()
    {
        Id = Guid.NewGuid(),
        ClaimNumber = "CLM-2026-0000001",
        Status = status,
        PolicyId = Guid.NewGuid(),
        Policy = new Policy { EffectiveDate = Now.AddYears(-1), ExpirationDate = Now.AddYears(1) },
        LossEvent = new LossEvent
        {
            LossDate = Now.AddDays(-10),
            LossDescription = "Kitchen fire spread to the living room",
            CauseOfLossCode = "COL-FIRE"
        },
        Parties = [Claimant()]
    };

    public static ClaimParty Claimant(bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        PartyRole = PartyRole.Claimant,
        PartyType = PartyType.Person,
        FirstName = "Ann",
        LastName = "Lee",
        IsActive = isActive
    };

    public static ClaimReserveComponent ApprovedReserve(Claim claim, ReserveComponentType type, decimal amount)
    {
        var component = claim.OpenReserveComponent(type);
        component.RecordTransaction(amount, null, HandlerId, requiresApproval: false);
        return component;
    }

    public static ReserveHistory PendingTransaction(Claim claim, ReserveComponentType type, decimal amount, Guid? submittedBy = null)
    {
        var component = claim.ReserveComponents.FirstOrDefault(rc => rc.Component == type) ?? claim.OpenReserveComponent(type);
        return component.RecordTransaction(amount, null, submittedBy ?? HandlerId, requiresApproval: true);
    }
}
