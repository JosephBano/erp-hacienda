using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.HealthPlans;

/// <summary>
/// Wire-format DTO for a <see cref="HealthPlan"/>.
/// </summary>
public record HealthPlanDto(
    Guid Id,
    string Name,
    Guid SpeciesId,
    bool IsActive,
    List<HealthPlanItemDto> Items);

public record HealthPlanItemDto(
    Guid Id,
    string Name,
    string EventType,
    string Anchor,
    int AnchorOffsetDays,
    int ComplianceWindowDays,
    Guid? InventoryItemId,
    Guid? RouteId,
    decimal? DoseQuantity,
    Guid? DoseUnitId,
    int? Repetitions,
    Guid? AppliesToCategoryId,
    string? AppliesToSex,
    bool IsActive);

public record HealthPlanAssignmentDto(
    Guid Id,
    Guid HealthPlanId,
    Guid? AnimalId,
    Guid? GroupId,
    DateTimeOffset AssignedAt,
    bool IsActive);

public record CreateHealthPlanCommand(string Name, Guid SpeciesId) : IRequest<Guid>;

public class CreateHealthPlanValidator : AbstractValidator<CreateHealthPlanCommand>
{
    public CreateHealthPlanValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.SpeciesId).NotEqual(Guid.Empty);
    }
}

public class CreateHealthPlanHandler(ILivestockDbContext dbContext)
    : IRequestHandler<CreateHealthPlanCommand, Guid>
{
    public async Task<Guid> Handle(CreateHealthPlanCommand request, CancellationToken cancellationToken)
    {
        var speciesExists = await dbContext.Species
            .AnyAsync(s => s.Id == request.SpeciesId && s.DeletedAt == null, cancellationToken);
        if (!speciesExists)
            throw new DomainException($"La especie '{request.SpeciesId}' no existe.");

        var duplicate = await dbContext.HealthPlans
            .AnyAsync(p => p.Name == request.Name.Trim()
                           && p.SpeciesId == request.SpeciesId,
                cancellationToken);
        if (duplicate)
            throw new DuplicateHealthPlanException(request.Name, request.SpeciesId);

        var plan = HealthPlan.Create(request.Name, request.SpeciesId);
        dbContext.HealthPlans.Add(plan);
        await dbContext.SaveChangesAsync(cancellationToken);

        return plan.Id;
    }
}

public class DuplicateHealthPlanException : Exception
{
    public string Name { get; }
    public Guid SpeciesId { get; }

    public DuplicateHealthPlanException(string name, Guid speciesId)
        : base($"Ya existe un plan sanitario con el nombre '{name}' para esta especie.")
    {
        Name = name;
        SpeciesId = speciesId;
    }
}

public record AddHealthPlanItemCommand(
    Guid HealthPlanId,
    string Name,
    string EventType,
    PlanAnchor Anchor,
    int AnchorOffsetDays,
    int ComplianceWindowDays,
    Guid? InventoryItemId = null,
    Guid? RouteId = null,
    decimal? DoseQuantity = null,
    Guid? DoseUnitId = null,
    int? Repetitions = null,
    Guid? AppliesToCategoryId = null,
    string? AppliesToSex = null) : IRequest<Guid>;

public class AddHealthPlanItemValidator : AbstractValidator<AddHealthPlanItemCommand>
{
    public AddHealthPlanItemValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.EventType).NotEmpty().MaximumLength(60);
        RuleFor(x => x.HealthPlanId).NotEqual(Guid.Empty);
    }
}

public class AddHealthPlanItemHandler(ILivestockDbContext dbContext)
    : IRequestHandler<AddHealthPlanItemCommand, Guid>
{
    public async Task<Guid> Handle(AddHealthPlanItemCommand request, CancellationToken cancellationToken)
    {
        var plan = await dbContext.HealthPlans.FirstOrDefaultAsync(p => p.Id == request.HealthPlanId, cancellationToken);
        if (plan is null)
            throw new DomainException($"El plan sanitario '{request.HealthPlanId}' no existe.");

        var item = HealthPlanItem.Create(
            healthPlanId: request.HealthPlanId,
            name: request.Name,
            eventType: request.EventType,
            anchor: request.Anchor,
            anchorOffsetDays: request.AnchorOffsetDays,
            complianceWindowDays: request.ComplianceWindowDays,
            inventoryItemId: request.InventoryItemId,
            routeId: request.RouteId,
            doseQuantity: request.DoseQuantity,
            doseUnitId: request.DoseUnitId,
            repetitions: request.Repetitions,
            appliesToCategoryId: request.AppliesToCategoryId,
            appliesToSex: request.AppliesToSex);

        dbContext.HealthPlanItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}

public record AssignHealthPlanCommand(
    Guid HealthPlanId,
    Guid? AnimalId,
    Guid? GroupId) : IRequest<Guid>;

public class AssignHealthPlanHandler(ILivestockDbContext dbContext)
    : IRequestHandler<AssignHealthPlanCommand, Guid>
{
    public async Task<Guid> Handle(AssignHealthPlanCommand request, CancellationToken cancellationToken)
    {
        var planExists = await dbContext.HealthPlans
            .AnyAsync(p => p.Id == request.HealthPlanId && p.DeletedAt == null, cancellationToken);
        if (!planExists)
            throw new DomainException($"El plan sanitario '{request.HealthPlanId}' no existe.");

        var assignment = HealthPlanAssignment.Create(
            request.HealthPlanId,
            request.AnimalId,
            request.GroupId);

        dbContext.HealthPlanAssignments.Add(assignment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return assignment.Id;
    }
}

public record GetHealthPlansQuery(
    Guid? SpeciesId = null,
    bool IncludeInactive = false) : IRequest<List<HealthPlanDto>>;

public class GetHealthPlansHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetHealthPlansQuery, List<HealthPlanDto>>
{
    public async Task<List<HealthPlanDto>> Handle(GetHealthPlansQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.HealthPlans.AsNoTracking().AsQueryable();

        if (request.SpeciesId is { } speciesId)
            query = query.Where(p => p.SpeciesId == speciesId);

        if (!request.IncludeInactive)
            query = query.Where(p => p.IsActive);

        var plans = await query
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var planIds = plans.Select(p => p.Id).ToList();
        var items = await dbContext.HealthPlanItems
            .AsNoTracking()
            .Where(i => planIds.Contains(i.HealthPlanId))
            .OrderBy(i => i.Name)
            .ToListAsync(cancellationToken);

        return plans.Select(p => new HealthPlanDto(
            p.Id,
            p.Name,
            p.SpeciesId,
            p.IsActive,
            items
                .Where(i => i.HealthPlanId == p.Id)
                .Select(i => new HealthPlanItemDto(
                    i.Id,
                    i.Name,
                    i.EventType,
                    i.Anchor.ToString(),
                    i.AnchorOffsetDays,
                    i.ComplianceWindowDays,
                    i.InventoryItemId,
                    i.RouteId,
                    i.DoseQuantity,
                    i.DoseUnitId,
                    i.Repetitions,
                    i.AppliesToCategoryId,
                    i.AppliesToSex,
                    i.IsActive))
                .ToList()))
            .ToList();
    }
}
