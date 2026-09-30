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
    DateTimeOffset? UpdatedAt,
    string? ParentKey = null);

public record GetFarmModulesQuery() : IRequest<List<FarmModuleDto>>;

public class GetFarmModulesQueryHandler(IPeopleDbContext dbContext)
    : IRequestHandler<GetFarmModulesQuery, List<FarmModuleDto>>
{
    public async Task<List<FarmModuleDto>> Handle(GetFarmModulesQuery request, CancellationToken cancellationToken)
    {
        var modules = await dbContext.FarmModules
            .AsNoTracking()
            .OrderBy(m => m.Key)
            .ToListAsync(cancellationToken);

        return modules
            .Select(m => new FarmModuleDto(
                m.Id,
                m.Key,
                m.Enabled,
                m.DisabledReason,
                m.CreatedAt,
                m.UpdatedAt,
                m.ParentKey))
            .ToList();
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
        var normalizedKey = request.Key.Trim().ToLowerInvariant();
        var module = await dbContext.FarmModules
            .FirstOrDefaultAsync(m => m.Key == normalizedKey, cancellationToken);

        if (module is null)
        {
            module = FarmModule.Create(request.Key, request.Enabled, request.DisabledReason);
            dbContext.FarmModules.Add(module);
        }
        else
        {
            module.SetEnabled(request.Enabled, request.DisabledReason);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new FarmModuleDto(
            module.Id,
            module.Key,
            module.Enabled,
            module.DisabledReason,
            module.CreatedAt,
            module.UpdatedAt,
            module.ParentKey);
    }
}
