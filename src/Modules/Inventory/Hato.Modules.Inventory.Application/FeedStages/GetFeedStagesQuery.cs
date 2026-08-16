using Hato.Modules.Inventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Inventory.Application.FeedStages;

public record FeedStageDto(Guid Id, string Key, string LabelEs, bool IsActive);

/// <summary>
/// Lists the feed stage catalog (docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.5 task 3). Mirrors
/// <c>GetAdministrationRoutesQuery</c>/<c>GetMortalityCausesQuery</c>: active-only by
/// default, `includeInactive` for the panel's "show retired rows" case.
/// </summary>
public record GetFeedStagesQuery(bool IncludeInactive = false) : IRequest<List<FeedStageDto>>;

public class GetFeedStagesHandler(IInventoryDbContext dbContext)
    : IRequestHandler<GetFeedStagesQuery, List<FeedStageDto>>
{
    public async Task<List<FeedStageDto>> Handle(GetFeedStagesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.FeedStages.AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(s => s.IsActive);

        return await query
            .OrderBy(s => s.LabelEs)
            .Select(s => new FeedStageDto(s.Id, s.Key, s.LabelEs, s.IsActive))
            .ToListAsync(cancellationToken);
    }
}
