using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.TreatmentCatalogs.AdministrationRoutes;

public record AdministrationRouteDto(
    Guid Id,
    string Key,
    string LabelEs,
    bool IsActive,
    Guid? DefaultUnitId);

public record CreateAdministrationRouteCommand(string Key, string LabelEs) : IRequest<Guid>;

public class CreateAdministrationRouteValidator : AbstractValidator<CreateAdministrationRouteCommand>
{
    public CreateAdministrationRouteValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LabelEs).NotEmpty().MaximumLength(100);
    }
}

public class CreateAdministrationRouteHandler(ILivestockDbContext dbContext)
    : IRequestHandler<CreateAdministrationRouteCommand, Guid>
{
    public async Task<Guid> Handle(CreateAdministrationRouteCommand request, CancellationToken cancellationToken)
    {
        var route = AdministrationRoute.Create(request.Key, request.LabelEs);

        // Defensive uniqueness check: the database already enforces this via the
        // filtered unique index, but a friendly Problem Details beats a raw
        // constraint violation for the panel user.
        var keyTaken = await dbContext.AdministrationRoutes
            .AsNoTracking()
            .AnyAsync(r => r.Key == route.Key, cancellationToken);

        if (keyTaken)
            throw new DomainException($"Ya existe una vía de administración con la clave '{route.Key}'.");

        dbContext.AdministrationRoutes.Add(route);
        await dbContext.SaveChangesAsync(cancellationToken);

        return route.Id;
    }
}

public record DeactivateAdministrationRouteCommand(Guid Id) : IRequest;

public class DeactivateAdministrationRouteHandler(ILivestockDbContext dbContext)
    : IRequestHandler<DeactivateAdministrationRouteCommand>
{
    public async Task Handle(DeactivateAdministrationRouteCommand request, CancellationToken cancellationToken)
    {
        var route = await dbContext.AdministrationRoutes
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (route is null)
            throw new DomainException($"La vía de administración con ID '{request.Id}' no existe.");

        route.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public record ActivateAdministrationRouteCommand(Guid Id) : IRequest;

public class ActivateAdministrationRouteHandler(ILivestockDbContext dbContext)
    : IRequestHandler<ActivateAdministrationRouteCommand>
{
    public async Task Handle(ActivateAdministrationRouteCommand request, CancellationToken cancellationToken)
    {
        var route = await dbContext.AdministrationRoutes
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (route is null)
            throw new DomainException($"La vía de administración con ID '{request.Id}' no existe.");

        route.Activate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public record UpdateAdministrationRouteLabelCommand(Guid Id, string LabelEs) : IRequest;

public class UpdateAdministrationRouteLabelValidator : AbstractValidator<UpdateAdministrationRouteLabelCommand>
{
    public UpdateAdministrationRouteLabelValidator()
    {
        RuleFor(x => x.LabelEs).NotEmpty().MaximumLength(100);
    }
}

public class UpdateAdministrationRouteLabelHandler(ILivestockDbContext dbContext)
    : IRequestHandler<UpdateAdministrationRouteLabelCommand>
{
    public async Task Handle(UpdateAdministrationRouteLabelCommand request, CancellationToken cancellationToken)
    {
        var route = await dbContext.AdministrationRoutes
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (route is null)
            throw new DomainException($"La vía de administración con ID '{request.Id}' no existe.");

        route.UpdateLabel(request.LabelEs);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public record GetAdministrationRoutesQuery(bool IncludeInactive = false)
    : IRequest<List<AdministrationRouteDto>>;

public class GetAdministrationRoutesHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetAdministrationRoutesQuery, List<AdministrationRouteDto>>
{
    public async Task<List<AdministrationRouteDto>> Handle(
        GetAdministrationRoutesQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AdministrationRoutes.AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(r => r.IsActive);

        return await query
            .OrderBy(r => r.LabelEs)
            .Select(r => new AdministrationRouteDto(r.Id, r.Key, r.LabelEs, r.IsActive, r.DefaultUnitId))
            .ToListAsync(cancellationToken);
    }
}