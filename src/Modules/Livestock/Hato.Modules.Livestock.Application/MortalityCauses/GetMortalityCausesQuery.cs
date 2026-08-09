using Hato.Modules.Livestock.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.MortalityCauses;

public record MortalityCauseDto(Guid Id, string Name, bool IsActive);

public record GetMortalityCausesQuery(bool IncludeInactive = false) : IRequest<List<MortalityCauseDto>>;

public class GetMortalityCausesHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetMortalityCausesQuery, List<MortalityCauseDto>>
{
    public async Task<List<MortalityCauseDto>> Handle(GetMortalityCausesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.MortalityCauses.AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new MortalityCauseDto(c.Id, c.Name, c.IsActive))
            .ToListAsync(cancellationToken);
    }
}
