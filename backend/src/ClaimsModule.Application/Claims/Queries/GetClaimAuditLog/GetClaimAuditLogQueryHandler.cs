using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;

public class GetClaimAuditLogQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetClaimAuditLogQuery, PagedResult<AuditLogEntryDto>>
{
    public async Task<PagedResult<AuditLogEntryDto>> Handle(GetClaimAuditLogQuery request, CancellationToken cancellationToken)
    {
        if (!await context.Claims.AnyAsync(c => c.Id == request.ClaimId, cancellationToken))
        {
            throw new NotFoundException(nameof(Claim), request.ClaimId);
        }

        var entries = context.ClaimAuditLog
            .AsNoTracking()
            .Where(a => a.ClaimId == request.ClaimId);

        var totalCount = await entries.CountAsync(cancellationToken);

        var items = await entries
            .OrderByDescending(a => a.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AuditLogEntryDto(
                a.Id,
                a.EventType,
                a.Description,
                a.OldValue,
                a.NewValue,
                a.RelatedEntityId,
                a.RelatedEntityType,
                a.CorrelationId,
                a.CreatedAt,
                a.UserCreated))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogEntryDto>(items, totalCount, request.Page, request.PageSize);
    }
}
