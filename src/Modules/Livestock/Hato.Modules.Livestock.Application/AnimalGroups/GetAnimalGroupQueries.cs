using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.AnimalGroups;

public record GroupMembershipDto(Guid Id, Guid AnimalId, DateOnly JoinedAt, DateOnly? LeftAt, bool IsActive);

public record AnimalGroupDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? SpeciesId,
    bool IsActive,
    TrackingMode TrackingMode,
    List<GroupMembershipDto> Memberships);

public record GetAnimalGroupByIdQuery(Guid Id) : IRequest<AnimalGroupDto>;

public record GetAnimalGroupsQuery(bool IncludeInactive = false) : IRequest<List<AnimalGroupDto>>;

public class GetAnimalGroupByIdHandler(ILivestockDbContext dbContext) : IRequestHandler<GetAnimalGroupByIdQuery, AnimalGroupDto>
{
    public async Task<AnimalGroupDto> Handle(GetAnimalGroupByIdQuery request, CancellationToken cancellationToken)
    {
        var group = await dbContext.AnimalGroups
            .AsNoTracking()
            .Include(g => g.Memberships)
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);

        if (group is null)
            throw new DomainException($"El grupo con ID '{request.Id}' no existe.");

        return new AnimalGroupDto(
            group.Id,
            group.Name,
            group.Description,
            group.SpeciesId,
            group.IsActive,
            group.TrackingMode,
            group.Memberships.Select(m => new GroupMembershipDto(m.Id, m.AnimalId, m.JoinedAt, m.LeftAt, m.IsActive)).ToList());
    }
}

public class GetAnimalGroupsHandler(ILivestockDbContext dbContext) : IRequestHandler<GetAnimalGroupsQuery, List<AnimalGroupDto>>
{
    public async Task<List<AnimalGroupDto>> Handle(GetAnimalGroupsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.AnimalGroups
            .AsNoTracking()
            .Include(g => g.Memberships)
            .AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(g => g.IsActive);

        var groups = await query.ToListAsync(cancellationToken);

        return groups.Select(group => new AnimalGroupDto(
            group.Id,
            group.Name,
            group.Description,
            group.SpeciesId,
            group.IsActive,
            group.TrackingMode,
            group.Memberships.Select(m => new GroupMembershipDto(m.Id, m.AnimalId, m.JoinedAt, m.LeftAt, m.IsActive)).ToList()
        )).ToList();
    }
}
