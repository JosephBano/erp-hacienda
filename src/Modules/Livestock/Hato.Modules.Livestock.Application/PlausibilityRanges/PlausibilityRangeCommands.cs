using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.PlausibilityRanges;

/// <summary>
/// Wire-format DTO for a <see cref="PlausibilityRange"/>.
/// <see cref="CategoryId"/> is null when the range applies to the whole species.
/// Bounds are null when not configured (the evaluator handles nulls as fail-open).
/// </summary>
public record PlausibilityRangeDto(
    Guid Id,
    Guid SpeciesId,
    Guid? CategoryId,
    string Magnitude,
    decimal? PlausibleMin,
    decimal? PlausibleMax,
    decimal? AbsoluteMin,
    decimal? AbsoluteMax,
    bool IsActive);

public record CreatePlausibilityRangeCommand(
    Guid SpeciesId,
    Guid? CategoryId,
    string Magnitude,
    decimal? PlausibleMin,
    decimal? PlausibleMax,
    decimal? AbsoluteMin,
    decimal? AbsoluteMax) : IRequest<Guid>;

public class CreatePlausibilityRangeValidator : AbstractValidator<CreatePlausibilityRangeCommand>
{
    public CreatePlausibilityRangeValidator()
    {
        RuleFor(x => x.Magnitude).NotEmpty().MaximumLength(50).Matches("^[a-z0-9_]+$")
            .WithMessage("La magnitud debe estar en minúsculas, sin espacios, con letras ASCII, dígitos y guión bajo.");
        RuleFor(x => x.SpeciesId).NotEqual(Guid.Empty);
    }
}

public class CreatePlausibilityRangeHandler(ILivestockDbContext dbContext)
    : IRequestHandler<CreatePlausibilityRangeCommand, Guid>
{
    public async Task<Guid> Handle(CreatePlausibilityRangeCommand request, CancellationToken cancellationToken)
    {
        // Verify the species exists. The FK constraint catches this at insert
        // time, but a friendly 400 here is better than a 500 with a Postgres
        // exception.
        var speciesExists = await dbContext.Species
            .AnyAsync(s => s.Id == request.SpeciesId && s.DeletedAt == null, cancellationToken);
        if (!speciesExists)
            throw new DomainException($"La especie '{request.SpeciesId}' no existe.");

        if (request.CategoryId is { } categoryId)
        {
            var categoryExists = await dbContext.AnimalCategories
                .AnyAsync(c => c.Id == categoryId && c.DeletedAt == null, cancellationToken);
            if (!categoryExists)
                throw new DomainException($"La categoría '{categoryId}' no existe.");
        }

        // Pre-check uniqueness so the duplicate is detectable here as a 400 with
        // a friendly message. The DB still enforces the constraint — this is a
        // mapping, not a workaround.
        var duplicate = await dbContext.PlausibilityRanges
            .AnyAsync(r => r.SpeciesId == request.SpeciesId
                           && r.CategoryId == request.CategoryId
                           && r.Magnitude == request.Magnitude,
                cancellationToken);
        if (duplicate)
            throw new DuplicatePlausibilityRangeException(
                request.SpeciesId, request.CategoryId, request.Magnitude);

        var range = PlausibilityRange.Create(
            request.SpeciesId,
            request.CategoryId,
            request.Magnitude,
            request.PlausibleMin,
            request.PlausibleMax,
            request.AbsoluteMin,
            request.AbsoluteMax);

        dbContext.PlausibilityRanges.Add(range);
        await dbContext.SaveChangesAsync(cancellationToken);

        return range.Id;
    }
}

/// <summary>
/// Thrown when a (species, category, magnitude) combination already exists.
/// Mapped to HTTP 409 by <see cref="ApiExceptionHandler"/>.
/// </summary>
public class DuplicatePlausibilityRangeException : Exception
{
    public Guid SpeciesId { get; }
    public Guid? CategoryId { get; }
    public string Magnitude { get; }

    public DuplicatePlausibilityRangeException(Guid speciesId, Guid? categoryId, string magnitude)
        : base($"Ya existe un rango de plausibilidad para la combinación especie='{speciesId}', " +
               $"categoría='{categoryId?.ToString() ?? "(todas)"}', magnitud='{magnitude}'.")
    {
        SpeciesId = speciesId;
        CategoryId = categoryId;
        Magnitude = magnitude;
    }
}

public record UpdatePlausibilityRangeBoundsCommand(
    Guid Id,
    decimal? PlausibleMin,
    decimal? PlausibleMax,
    decimal? AbsoluteMin,
    decimal? AbsoluteMax) : IRequest;

public class UpdatePlausibilityRangeBoundsHandler(ILivestockDbContext dbContext)
    : IRequestHandler<UpdatePlausibilityRangeBoundsCommand>
{
    public async Task Handle(UpdatePlausibilityRangeBoundsCommand request, CancellationToken cancellationToken)
    {
        var range = await dbContext.PlausibilityRanges.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (range is null)
            throw new DomainException($"El rango de plausibilidad con ID '{request.Id}' no existe.");

        range.UpdateBounds(
            request.PlausibleMin,
            request.PlausibleMax,
            request.AbsoluteMin,
            request.AbsoluteMax);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public record DeactivatePlausibilityRangeCommand(Guid Id) : IRequest;

public class DeactivatePlausibilityRangeHandler(ILivestockDbContext dbContext)
    : IRequestHandler<DeactivatePlausibilityRangeCommand>
{
    public async Task Handle(DeactivatePlausibilityRangeCommand request, CancellationToken cancellationToken)
    {
        var range = await dbContext.PlausibilityRanges.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (range is null)
            throw new DomainException($"El rango de plausibilidad con ID '{request.Id}' no existe.");

        range.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public record ActivatePlausibilityRangeCommand(Guid Id) : IRequest;

public class ActivatePlausibilityRangeHandler(ILivestockDbContext dbContext)
    : IRequestHandler<ActivatePlausibilityRangeCommand>
{
    public async Task Handle(ActivatePlausibilityRangeCommand request, CancellationToken cancellationToken)
    {
        var range = await dbContext.PlausibilityRanges.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (range is null)
            throw new DomainException($"El rango de plausibilidad con ID '{request.Id}' no existe.");

        range.Activate();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public record GetPlausibilityRangesQuery(
    Guid? SpeciesId = null,
    Guid? CategoryId = null,
    string? Magnitude = null,
    bool IncludeInactive = false) : IRequest<List<PlausibilityRangeDto>>;

public class GetPlausibilityRangesHandler(ILivestockDbContext dbContext)
    : IRequestHandler<GetPlausibilityRangesQuery, List<PlausibilityRangeDto>>
{
    public async Task<List<PlausibilityRangeDto>> Handle(GetPlausibilityRangesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.PlausibilityRanges.AsNoTracking().AsQueryable();

        if (request.SpeciesId is { } speciesId)
            query = query.Where(r => r.SpeciesId == speciesId);

        if (request.CategoryId is { } categoryId)
            query = query.Where(r => r.CategoryId == categoryId);

        if (!string.IsNullOrWhiteSpace(request.Magnitude))
            query = query.Where(r => r.Magnitude == request.Magnitude);

        if (!request.IncludeInactive)
            query = query.Where(r => r.IsActive);

        return await query
            .OrderBy(r => r.SpeciesId)
            .ThenBy(r => r.CategoryId)
            .ThenBy(r => r.Magnitude)
            .Select(r => new PlausibilityRangeDto(
                r.Id,
                r.SpeciesId,
                r.CategoryId,
                r.Magnitude,
                r.PlausibleMin,
                r.PlausibleMax,
                r.AbsoluteMin,
                r.AbsoluteMax,
                r.IsActive))
            .ToListAsync(cancellationToken);
    }
}
