using System.Linq.Expressions;
using ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Documents;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Application.Claims.Queries.GetClaimDetail;

internal record ClaimDetailProjection(Guid Id, string ClaimNumber, ClaimStatus Status, Severity? Severity, Guid? PolicyId, string? PolicyNumber, string? ClientName, DateTimeOffset ReportedDate, Guid? AssignedHandlerId, DateTimeOffset? ClosedAt, string? ClosureReason, string? Notes, LossEventDto? LossEvent, IReadOnlyList<ClaimPartyDto> Parties, IReadOnlyList<ClaimRiskObjectDto> RiskObjects, IReadOnlyList<ReserveComponentSummaryDto> ReserveComponents, IReadOnlyList<ClaimDocumentDto> Documents, IReadOnlyList<AuditLogEntryDto> RecentAuditEntries);

internal static class ClaimDetailProjections
{
    private const int RecentAuditEntryCount = 10;

    private static readonly Expression<Func<ClaimParty, ClaimPartyDto>> Party = p => new ClaimPartyDto(
        p.Id,
        p.PartyRole,
        p.PartyType,
        p.FirstName,
        p.LastName,
        p.CompanyName,
        p.Email,
        p.Phone,
        p.Notes,
        p.IsActive);

    private static readonly Expression<Func<ClaimRiskObject, ClaimRiskObjectDto>> RiskObject = r => new ClaimRiskObjectDto(
        r.Id,
        r.AssetType,
        r.AssetDescription,
        r.DamageDescription,
        r.IsPrimary,
        r.AssetReference);

    private static readonly Expression<Func<ClaimReserveComponent, ReserveComponentSummaryDto>> ReserveComponent = rc => new ReserveComponentSummaryDto(
        rc.Id,
        rc.Component,
        rc.Status,
        rc.History
            .Where(h => h.ApprovalStatus == ReserveApprovalStatus.Approved || h.ApprovalStatus == ReserveApprovalStatus.AutoApproved)
            .Sum(h => (decimal?)h.Amount) ?? 0,
        rc.History
            .Where(h => h.ApprovalStatus == ReserveApprovalStatus.PendingApproval)
            .Sum(h => (decimal?)h.Amount) ?? 0);

    private static readonly Expression<Func<ClaimDocument, ClaimDocumentDto>> Document = d => new ClaimDocumentDto(
        d.Id,
        d.DocumentType,
        d.DocumentName,
        d.ContentType,
        d.FileSizeBytes,
        d.UploadedAt,
        d.UploadedByUserId);

    private static readonly Expression<Func<ClaimAuditLog, AuditLogEntryDto>> AuditEntry = a => new AuditLogEntryDto(
        a.Id,
        a.EventType,
        a.Description,
        a.OldValue,
        a.NewValue,
        a.RelatedEntityId,
        a.RelatedEntityType,
        a.CorrelationId,
        a.CreatedAt,
        a.UserCreated);

    public static readonly Expression<Func<Claim, ClaimDetailProjection>> Claim = c => new ClaimDetailProjection(
        c.Id,
        c.ClaimNumber,
        c.Status,
        c.Severity,
        c.PolicyId,
        c.PolicyNumber,
        c.ClientName,
        c.ReportedDate,
        c.AssignedHandlerId,
        c.ClosedAt,
        c.ClosureReason,
        c.Notes,
        c.LossEvent == null ? null : new LossEventDto(
            c.LossEvent.LossDate,
            c.LossEvent.LossDescription,
            c.LossEvent.LossLocation,
            c.LossEvent.CauseOfLossCode,
            c.LossEvent.CauseOfLossCodeReference.Name,
            c.LossEvent.EstimatedLossAmount,
            c.LossEvent.ReportDate,
            c.LossEvent.PoliceReportNumber),
        c.Parties.AsQueryable()
            .OrderBy(p => p.CreatedAt)
            .Select(Party)
            .ToList(),
        c.RiskObjects.AsQueryable()
            .OrderByDescending(r => r.IsPrimary)
            .ThenBy(r => r.CreatedAt)
            .Select(RiskObject)
            .ToList(),
        c.ReserveComponents.AsQueryable()
            .OrderBy(rc => rc.Component)
            .Select(ReserveComponent)
            .ToList(),
        c.Documents.AsQueryable()
            .OrderByDescending(d => d.UploadedAt)
            .Select(Document)
            .ToList(),
        c.AuditLogEntries.AsQueryable()
            .OrderByDescending(a => a.CreatedAt)
            .Take(RecentAuditEntryCount)
            .Select(AuditEntry)
            .ToList());
}
