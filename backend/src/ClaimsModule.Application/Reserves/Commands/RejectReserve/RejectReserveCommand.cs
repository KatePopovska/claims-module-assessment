using MediatR;

namespace ClaimsModule.Application.Reserves.Commands.RejectReserve;

public record RejectReserveCommand(Guid ClaimId, Guid TransactionId, string RejectionReason) : IRequest<ReserveTransactionDto>;
