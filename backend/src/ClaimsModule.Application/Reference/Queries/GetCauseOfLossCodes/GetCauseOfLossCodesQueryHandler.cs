using ClaimsModule.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Reference.Queries.GetCauseOfLossCodes;

public class GetCauseOfLossCodesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCauseOfLossCodesQuery, IReadOnlyList<CauseOfLossCodeDto>>
{
    public async Task<IReadOnlyList<CauseOfLossCodeDto>> Handle(GetCauseOfLossCodesQuery request, CancellationToken cancellationToken)
    {
        return await context.CauseOfLossCodes
            .AsNoTracking()
            .Where(c => c.IsActive && (!request.PerilCategory.HasValue || c.PerilCategory == request.PerilCategory))
            .OrderBy(c => c.SortOrder)
            .Select(c => new CauseOfLossCodeDto(c.Code, c.Name, c.PerilCategory, c.SortOrder))
            .ToListAsync(cancellationToken);
    }
}
