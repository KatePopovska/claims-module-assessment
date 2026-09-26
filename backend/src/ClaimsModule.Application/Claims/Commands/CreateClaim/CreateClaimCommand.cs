using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.CreateClaim;

public record CreateClaimCommand(Guid? PolicyId, DateTimeOffset LossDate, string LossDescription, string? LossLocation, string CauseOfLossCode,
    decimal? EstimatedLossAmount, string? PoliceReportNumber, IReadOnlyCollection<CreateClaimPartyDto> Parties,
    IReadOnlyCollection<CreateClaimRiskObjectDto> RiskObjects, CreateClaimInitialReserveDto? InitialReserve = null) : IRequest<ClaimCreatedDto>;

public record CreateClaimPartyDto(PartyRole PartyRole, PartyType PartyType, string? FirstName, string? LastName, string? CompanyName, string? Email, string? Phone);

public record CreateClaimRiskObjectDto(AssetType AssetType, string AssetDescription, string? DamageDescription, bool IsPrimary, string? AssetReference);

public record CreateClaimInitialReserveDto(ReserveComponentType Component, decimal Amount, string? ChangeReason);
