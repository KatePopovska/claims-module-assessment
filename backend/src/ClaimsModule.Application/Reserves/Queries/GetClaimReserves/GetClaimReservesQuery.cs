using ClaimsModule.Application.Claims.Queries.GetClaimDetail;
using MediatR;

namespace ClaimsModule.Application.Reserves.Queries.GetClaimReserves;

public record GetClaimReservesQuery(Guid ClaimId) : IRequest<ClaimReservesDto>;

public record ClaimReservesDto(decimal TotalReserves, bool ReserveLimitOverride, IReadOnlyList<ReserveComponentSummaryDto> Components, IReadOnlyList<ReserveTransactionDto> Transactions);
