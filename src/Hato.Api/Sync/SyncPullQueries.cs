using Hato.Modules.Inventory.Application.Abstractions;
using Hato.Modules.Livestock.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Api.Sync;

public record SyncPullResponseDto(
    string Cursor,
    bool HasMore,
    SyncCollectionsDto Collections);

public record SyncCollectionsDto(
    List<SyncAnimalDto> Animals,
    List<SyncAnimalIdentifierDto> AnimalIdentifiers,
    List<SyncAnimalGroupDto> AnimalGroups,
    List<SyncGroupMembershipDto> GroupMemberships,
    List<SyncSpeciesDto> Species,
    List<SyncBreedDto> Breeds,
    List<SyncCategoryDto> AnimalCategories,
    List<SyncInventoryItemDto> InventoryItems);

public record SyncAnimalDto(
    Guid Id,
    string Sex,
    DateOnly? BirthDate,
    Guid SpeciesId,
    Guid? BreedId,
    Guid? CategoryId,
    Guid? MotherId,
    Guid? FatherAnimalId,
    Guid? FatherStrawId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted);

public record SyncAnimalIdentifierDto(
    Guid Id,
    Guid AnimalId,
    string Type,
    string Value,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted);

public record SyncAnimalGroupDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? SpeciesId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted);

public record SyncGroupMembershipDto(
    Guid Id,
    Guid AnimalId,
    Guid GroupId,
    DateOnly JoinedAt,
    DateOnly? LeftAt,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted);

public record SyncSpeciesDto(
    Guid Id,
    string Name,
    int? GestationDays,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted);

public record SyncBreedDto(
    Guid Id,
    Guid SpeciesId,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted);

public record SyncCategoryDto(
    Guid Id,
    Guid SpeciesId,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted);

public record SyncInventoryItemDto(
    Guid Id,
    string Name,
    string Category,
    string Unit,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted);

public record GetSyncPullQuery(
    string? Since = null,
    string? Collections = null,
    int BatchSize = 500) : IRequest<SyncPullResponseDto>;

public class GetSyncPullQueryHandler(
    ILivestockDbContext livestockDb,
    IInventoryDbContext inventoryDb)
    : IRequestHandler<GetSyncPullQuery, SyncPullResponseDto>
{
    public async Task<SyncPullResponseDto> Handle(GetSyncPullQuery request, CancellationToken cancellationToken)
    {
        DateTimeOffset sinceTime = DateTimeOffset.MinValue;

        if (!string.IsNullOrWhiteSpace(request.Since))
        {
            var parts = request.Since.Split('_');
            if (parts.Length > 0 && DateTimeOffset.TryParse(parts[0], out var parsedTime))
            {
                sinceTime = parsedTime;
            }
        }

        var limit = request.BatchSize is < 1 or > 1000 ? 500 : request.BatchSize;

        // Pull Animals
        var animals = await livestockDb.Animals
            .IgnoreQueryFilters()
            .Where(a => a.CreatedAt >= sinceTime || (a.UpdatedAt.HasValue && a.UpdatedAt.Value >= sinceTime))
            .Take(limit)
            .Select(a => new SyncAnimalDto(
                a.Id,
                a.Sex.ToString(),
                a.BirthDate,
                a.SpeciesId,
                a.BreedId,
                a.CategoryId,
                a.MotherId,
                a.FatherAnimalId,
                a.FatherStrawId,
                a.CreatedAt,
                a.UpdatedAt,
                a.IsDeleted))
            .ToListAsync(cancellationToken);

        // Pull Identifiers
        var identifiers = await livestockDb.AnimalIdentifiers
            .IgnoreQueryFilters()
            .Where(i => i.CreatedAt >= sinceTime || (i.UpdatedAt.HasValue && i.UpdatedAt.Value >= sinceTime))
            .Take(limit)
            .Select(i => new SyncAnimalIdentifierDto(
                i.Id,
                i.AnimalId,
                i.Type.ToString(),
                i.Value,
                i.IsActive,
                i.CreatedAt,
                i.UpdatedAt,
                i.IsDeleted))
            .ToListAsync(cancellationToken);

        // Pull Animal Groups
        var groups = await livestockDb.AnimalGroups
            .IgnoreQueryFilters()
            .Where(g => g.CreatedAt >= sinceTime || (g.UpdatedAt.HasValue && g.UpdatedAt.Value >= sinceTime))
            .Take(limit)
            .Select(g => new SyncAnimalGroupDto(
                g.Id,
                g.Name,
                g.Description,
                g.SpeciesId,
                g.IsActive,
                g.CreatedAt,
                g.UpdatedAt,
                g.IsDeleted))
            .ToListAsync(cancellationToken);

        // Pull Memberships
        var memberships = await livestockDb.GroupMemberships
            .IgnoreQueryFilters()
            .Where(m => m.CreatedAt >= sinceTime || (m.UpdatedAt.HasValue && m.UpdatedAt.Value >= sinceTime))
            .Take(limit)
            .Select(m => new SyncGroupMembershipDto(
                m.Id,
                m.AnimalId,
                m.GroupId,
                m.JoinedAt,
                m.LeftAt,
                m.IsActive,
                m.CreatedAt,
                m.UpdatedAt,
                m.IsDeleted))
            .ToListAsync(cancellationToken);

        // Pull Species
        var speciesList = await livestockDb.Species
            .IgnoreQueryFilters()
            .Where(s => s.CreatedAt >= sinceTime || (s.UpdatedAt.HasValue && s.UpdatedAt.Value >= sinceTime))
            .Take(limit)
            .Select(s => new SyncSpeciesDto(
                s.Id,
                s.Name,
                s.GestationDays,
                s.CreatedAt,
                s.UpdatedAt,
                s.IsDeleted))
            .ToListAsync(cancellationToken);

        // Pull Breeds
        var breeds = await livestockDb.Breeds
            .IgnoreQueryFilters()
            .Where(b => b.CreatedAt >= sinceTime || (b.UpdatedAt.HasValue && b.UpdatedAt.Value >= sinceTime))
            .Take(limit)
            .Select(b => new SyncBreedDto(
                b.Id,
                b.SpeciesId,
                b.Name,
                b.CreatedAt,
                b.UpdatedAt,
                b.IsDeleted))
            .ToListAsync(cancellationToken);

        // Pull Categories
        var categories = await livestockDb.AnimalCategories
            .IgnoreQueryFilters()
            .Where(c => c.CreatedAt >= sinceTime || (c.UpdatedAt.HasValue && c.UpdatedAt.Value >= sinceTime))
            .Take(limit)
            .Select(c => new SyncCategoryDto(
                c.Id,
                c.SpeciesId,
                c.Name,
                c.CreatedAt,
                c.UpdatedAt,
                c.IsDeleted))
            .ToListAsync(cancellationToken);

        // Pull Inventory Items
        var items = await inventoryDb.InventoryItems
            .IgnoreQueryFilters()
            .Where(i => i.CreatedAt >= sinceTime || (i.UpdatedAt.HasValue && i.UpdatedAt.Value >= sinceTime))
            .Take(limit)
            .Select(i => new SyncInventoryItemDto(
                i.Id,
                i.Name,
                i.Category.ToString(),
                i.Unit,
                i.Description,
                i.CreatedAt,
                i.UpdatedAt,
                i.IsDeleted))
            .ToListAsync(cancellationToken);

        // Compute max cursor
        var allMaxTimestamps = new List<(DateTimeOffset Time, Guid Id)>();

        if (animals.Count > 0) allMaxTimestamps.Add((animals.Max(x => x.UpdatedAt ?? x.CreatedAt), animals.Max(x => x.Id)));
        if (identifiers.Count > 0) allMaxTimestamps.Add((identifiers.Max(x => x.UpdatedAt ?? x.CreatedAt), identifiers.Max(x => x.Id)));
        if (groups.Count > 0) allMaxTimestamps.Add((groups.Max(x => x.UpdatedAt ?? x.CreatedAt), groups.Max(x => x.Id)));
        if (memberships.Count > 0) allMaxTimestamps.Add((memberships.Max(x => x.UpdatedAt ?? x.CreatedAt), memberships.Max(x => x.Id)));
        if (speciesList.Count > 0) allMaxTimestamps.Add((speciesList.Max(x => x.UpdatedAt ?? x.CreatedAt), speciesList.Max(x => x.Id)));
        if (breeds.Count > 0) allMaxTimestamps.Add((breeds.Max(x => x.UpdatedAt ?? x.CreatedAt), breeds.Max(x => x.Id)));
        if (categories.Count > 0) allMaxTimestamps.Add((categories.Max(x => x.UpdatedAt ?? x.CreatedAt), categories.Max(x => x.Id)));
        if (items.Count > 0) allMaxTimestamps.Add((items.Max(x => x.UpdatedAt ?? x.CreatedAt), items.Max(x => x.Id)));

        var latest = allMaxTimestamps.OrderByDescending(x => x.Time).ThenByDescending(x => x.Id).FirstOrDefault();
        var nextCursor = latest.Time != DateTimeOffset.MinValue
            ? $"{latest.Time:O}_{latest.Id}"
            : (request.Since ?? string.Empty);

        var hasMore = animals.Count >= limit || identifiers.Count >= limit || groups.Count >= limit || items.Count >= limit;

        var collectionsDto = new SyncCollectionsDto(
            animals,
            identifiers,
            groups,
            memberships,
            speciesList,
            breeds,
            categories,
            items);

        return new SyncPullResponseDto(nextCursor, hasMore, collectionsDto);
    }
}
