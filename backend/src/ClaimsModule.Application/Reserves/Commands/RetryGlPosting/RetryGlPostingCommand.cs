using MediatR;

namespace ClaimsModule.Application.Reserves.Commands.RetryGlPosting;

public record RetryGlPostingCommand(Guid ClaimId, Guid TransactionId) : IRequest<ReserveTransactionDto>;
