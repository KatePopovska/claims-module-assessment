using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Commands.UpdateClaimStatus;

public class UpdateClaimStatusCommandHandler(
    IClaimRepository claimRepository,
    IApplicationDbContext context,
    IUnitOfWork unitOfWork,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : IRequestHandler<UpdateClaimStatusCommand, UpdateClaimStatusResult>
{
    public async Task<UpdateClaimStatusResult> Handle(UpdateClaimStatusCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var now = timeProvider.GetUtcNow();

        var isCauseOfLossCodeActive = claim.LossEvent is not null
            && await context.CauseOfLossCodes
                .AsNoTracking()
                .AnyAsync(c => c.Code == claim.LossEvent.CauseOfLossCode && c.IsActive, cancellationToken);

        var evaluation = ClaimStatusChangePolicy.Evaluate(
            claim,
            new StatusChangeRequest(request.TargetStatus, request.Reason, currentUserService.Role, request.AcknowledgeWarnings),
            now,
            isCauseOfLossCodeActive);

        if (evaluation.BlockingIssues.Count > 0)
        {
            throw new ValidationException(evaluation.BlockingIssues.Select(i => new ValidationFailure(i.Field, i.Message)));
        }

        foreach (var change in claim.ChangeStatus(request.TargetStatus, request.Reason, now))
        {
            LogStatusChange(claim, change, change.IsAutomatic ? null : request.Reason, evaluation.AcknowledgedWarnings);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateClaimStatusResult(claim.Id, claim.Status);
    }

    private void LogStatusChange(Claim claim, StatusChange change, string? reason, IReadOnlyList<string> acknowledgedWarnings)
    {
        var description = change.IsAutomatic
            ? $"Status changed automatically from {change.From} to {change.To}."
            : $"Status changed from {change.From} to {change.To}.";

        if (acknowledgedWarnings.Count > 0)
        {
            description += $" Acknowledged warnings: {string.Join(" ", acknowledgedWarnings)}";
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            description += $" Reason: {reason}";
        }

        auditLogService.Log(claim, AuditEventType.STATUS_CHANGED, description, oldValue: change.From.ToString(), newValue: change.To.ToString());

        switch (change.To)
        {
            case ClaimStatus.Closed:
                auditLogService.Log(claim, AuditEventType.CLAIM_CLOSED, "Claim closed.", newValue: reason);
                break;

            case ClaimStatus.Reopened:
                auditLogService.Log(claim, AuditEventType.CLAIM_REOPENED, "Claim reopened.", newValue: reason);
                break;
        }
    }
}
