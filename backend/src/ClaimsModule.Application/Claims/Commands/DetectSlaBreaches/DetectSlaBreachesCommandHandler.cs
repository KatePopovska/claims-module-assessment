using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Commands.DetectSlaBreaches;

public class DetectSlaBreachesCommandHandler(
    IApplicationDbContext context,
    IUnitOfWork unitOfWork,
    IAuditLogService auditLogService,
    TimeProvider timeProvider) : IRequestHandler<DetectSlaBreachesCommand, int>
{
    public async Task<int> Handle(DetectSlaBreachesCommand request, CancellationToken cancellationToken)
    {
        var claims = await context.Claims
            .Where(ClaimSla.IsBreachDue(timeProvider.GetUtcNow()))
            .ToListAsync(cancellationToken);

        if (claims.Count == 0)
        {
            return 0;
        }

        foreach (var claim in claims)
        {
            auditLogService.Log(claim, AuditEventType.SLA_BREACH_DETECTED, ClaimSla.BreachDescription);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return claims.Count;
    }
}
