using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;

public record GetClaimAuditLogQuery(Guid ClaimId, int Page = 1, int PageSize = 20) : IRequest<PagedResult<AuditLogEntryDto>>;

public record AuditLogEntryDto(Guid Id, AuditEventType EventType, string Description, string? OldValue, string? NewValue, Guid? RelatedEntityId, string? RelatedEntityType, string? CorrelationId, DateTimeOffset CreatedAt, Guid? CreatedByUserId);
