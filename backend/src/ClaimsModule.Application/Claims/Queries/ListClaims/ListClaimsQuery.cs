using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries.ListClaims;

public record ListClaimsQuery(ClaimStatus[]? Status, DateTimeOffset? DateFrom, DateTimeOffset? DateTo, Guid? AssignedHandlerId, string? CauseOfLossCode, Guid? PolicyId, string? Search, int Page = 1, int PageSize = 20) : IRequest<PagedResult<ClaimSummaryDto>>;

public record ClaimSummaryDto(Guid Id, string ClaimNumber, string? PolicyNumber, string? ClientName, DateTimeOffset? LossDate, string? CauseOfLossCode, string? CauseOfLossName, ClaimStatus Status, Guid? AssignedHandlerId, decimal TotalReserves);
