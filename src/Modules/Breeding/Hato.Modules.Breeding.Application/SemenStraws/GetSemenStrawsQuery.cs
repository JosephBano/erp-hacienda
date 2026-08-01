using Hato.Modules.Breeding.Application.Abstractions;
using Hato.Modules.Breeding.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Breeding.Application.SemenStraws;

public record GetSemenStrawsQuery() : IRequest<List<SemenStrawDto>>;

public class GetSemenStrawsQueryHandler(IBreedingDbContext dbContext)
    : IRequestHandler<GetSemenStrawsQuery, List<SemenStrawDto>>
{
    public async Task<List<SemenStrawDto>> Handle(GetSemenStrawsQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.SemenStraws
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SemenStrawDto(
                s.Id,
                s.Code,
                s.BullName,
                s.BullCode,
                s.BreedId,
                s.SupplierName,
                s.InitialQuantity,
                s.CurrentQuantity,
                s.Notes,
                s.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
