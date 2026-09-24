using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Reference.Queries.GetCauseOfLossCodes;

public record GetCauseOfLossCodesQuery(PerilCategory? PerilCategory) : IRequest<IReadOnlyList<CauseOfLossCodeDto>>;

public record CauseOfLossCodeDto(string Code, string Name, PerilCategory PerilCategory, int SortOrder);
