using MediatR;

namespace ClaimsModule.Application.Reserves.Commands.ApproveReserve;

public record ApproveReserveCommand(Guid ClaimId, Guid TransactionId) : IRequest<ReserveTransactionDto>;
