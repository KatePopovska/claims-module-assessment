using System.Globalization;
using ClaimsModule.Application.Common.Extensions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Application.Reserves;

public class ReserveTransactionSubmitter(
    IUnitOfWork unitOfWork,
    IAuditLogService auditLogService,
    IGlPostingScheduler glPostingScheduler,
    ICurrentUserService currentUserService)
{
    public async Task<ReserveSubmissionResultDto> SubmitAsync(Claim claim, ClaimReserveComponent component, decimal amount, string? changeReason, CancellationToken cancellationToken)
    {
        var warnings = ReserveRules.GetSubmissionWarnings(claim, amount);
        var requiresApproval = !ReserveAuthority.IsWithinAutoApprovalLimit(amount) || warnings.Count > 0;

        var transaction = component.RecordTransaction(amount, changeReason, currentUserService.GetRequiredUserId(), requiresApproval);

        var amountText = amount.ToString("N2", CultureInfo.InvariantCulture);

        auditLogService.Log(claim, AuditEventType.RESERVE_CREATED, $"{component.Component} reserve {transaction.TransactionType} of {amountText} submitted ({transaction.ApprovalStatus}).", newValue: amountText, relatedEntityId: transaction.Id, relatedEntityType: nameof(ReserveHistory));

        if (!requiresApproval)
        {
            auditLogService.Log(claim, AuditEventType.RESERVE_AUTO_APPROVED, $"{component.Component} reserve {transaction.TransactionType} of {amountText} auto-approved.", newValue: amountText, relatedEntityId: transaction.Id, relatedEntityType: nameof(ReserveHistory));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (!requiresApproval)
        {
            glPostingScheduler.Enqueue(transaction);
        }

        return new ReserveSubmissionResultDto(ReserveProjections.ToDto(transaction), warnings);
    }
}
