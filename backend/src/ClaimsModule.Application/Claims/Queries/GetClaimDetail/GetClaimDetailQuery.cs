using ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries.GetClaimDetail;

public record GetClaimDetailQuery(Guid ClaimId) : IRequest<ClaimDetailDto>;

public record ClaimDetailDto(Guid Id, string ClaimNumber, ClaimStatus Status, IReadOnlyCollection<ClaimStatus> ValidNextStatuses, Severity? Severity, Guid? PolicyId, string? PolicyNumber, string? ClientName, DateTimeOffset ReportedDate, Guid? AssignedHandlerId, DateTimeOffset? ClosedAt, string? ClosureReason, string? Notes, LossEventDto? LossEvent, IReadOnlyList<ClaimPartyDto> Parties, IReadOnlyList<ClaimRiskObjectDto> RiskObjects, ReserveSummaryDto Reserves, IReadOnlyList<ClaimDocumentDto> Documents, IReadOnlyList<AuditLogEntryDto> RecentAuditEntries);

public record LossEventDto(DateTimeOffset LossDate, string LossDescription, string? LossLocation, string CauseOfLossCode, string? CauseOfLossName, decimal? EstimatedLossAmount, DateTimeOffset ReportDate, string? PoliceReportNumber);

public record ClaimPartyDto(Guid Id, PartyRole PartyRole, PartyType PartyType, string? FirstName, string? LastName, string? CompanyName, string? Email, string? Phone, string? Notes, bool IsActive);

public record ClaimRiskObjectDto(Guid Id, AssetType AssetType, string AssetDescription, string? DamageDescription, bool IsPrimary, string? AssetReference);

public record ReserveSummaryDto(decimal TotalReserves, IReadOnlyList<ReserveComponentSummaryDto> Components);

public record ReserveComponentSummaryDto(Guid Id, ReserveComponentType Component, ReserveComponentStatus Status, decimal CurrentBalance, decimal PendingAmount);

public record ClaimDocumentDto(Guid Id, string DocumentType, string DocumentName, string ContentType, long FileSizeBytes, DateTimeOffset UploadedAt, Guid? UploadedByUserId);
