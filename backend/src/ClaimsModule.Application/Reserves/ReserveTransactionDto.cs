using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Reserves;

public record ReserveTransactionDto(Guid Id, Guid ReserveComponentId, ReserveComponentType Component, ReserveTransactionType TransactionType, decimal Amount, decimal PreviousBalance, decimal NewBalance, ReserveApprovalStatus ApprovalStatus, ReservePostingStatus PostingStatus, int ChangeSequence, string? ChangeReason, Guid SubmittedByUserId, Guid? ApprovedByUserId, DateTimeOffset? ApprovedAt, Guid? RejectedByUserId, DateTimeOffset? RejectedAt, string? RejectionReason, DateTimeOffset CreatedAt);

public record ReserveSubmissionResultDto(ReserveTransactionDto Transaction, IReadOnlyList<string> Warnings);
