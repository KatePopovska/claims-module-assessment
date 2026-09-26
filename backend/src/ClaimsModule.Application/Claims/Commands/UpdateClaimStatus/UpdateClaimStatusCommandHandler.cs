using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Commands.UpdateClaimStatus;

public class UpdateClaimStatusCommandHandler(
    IClaimRepository claimRepository,
    IApplicationDbContext context,
    IUnitOfWork unitOfWork,
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

        claim.ChangeStatus(request.TargetStatus, request.Reason, now, evaluation.AcknowledgedWarnings);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateClaimStatusResult(claim.Id, claim.Status);
    }
}
