using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.People.Application.FarmModules;

public record FarmModuleDto(
    Guid Id,
    string Key,
    bool Enabled,
    string? DisabledReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public record GetFarmModulesQuery() : IRequest<List<FarmModuleDto>>;

public class GetFarmModulesQueryHandler(IPeopleDbContext dbContext)
    : IRequestHandler<GetFarmModulesQuery, List<FarmModuleDto>>
{
    public async Task<List<FarmModuleDto>> Handle(GetFarmModulesQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.FarmModules
            .AsNoTracking()
            .OrderBy(m => m.Key)
            .Select(m => new FarmModuleDto(m.Id, m.Key, m.Enabled, m.DisabledReason, m.CreatedAt, m.UpdatedAt))
            .ToListAsync(cancellationToken);
    }
}

public record SetFarmModuleEnabledCommand(
    string Key,
    bool Enabled,
    string? DisabledReason) : IRequest<FarmModuleDto>;

public class SetFarmModuleEnabledCommandHandler(IPeopleDbContext dbContext)
    : IRequestHandler<SetFarmModuleEnabledCommand, FarmModuleDto>
{
    public async Task<FarmModuleDto> Handle(SetFarmModuleEnabledCommand request, CancellationToken cancellationToken)
    {
        var module = await dbContext.FarmModules
            .FirstOrDefaultAsync(m => m.Key == request.Key.ToLowerInvariant(), cancellationToken);

        if (module is null)
        {
            // The phone may have created a row already (the in-app toggle creates
            // a row on first use). The server, however, always has the seeded set;
            // an unknown key is therefore a configuration error, not a "create on
            // first use", because the canonical list lives in the migration.
            throw new DomainException(
                $"El módulo '{request.Key}' no está registrado. La lista de módulos la fija la migración inicial.");
        }

        module.SetEnabled(request.Enabled, request.DisabledReason);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new FarmModuleDto(module.Id, module.Key, module.Enabled, module.DisabledReason, module.CreatedAt, module.UpdatedAt);
    }
}
