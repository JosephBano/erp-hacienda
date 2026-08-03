using System.Linq.Expressions;
using Hato.Modules.Inventory.Application.Abstractions;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.People.Application.Abstractions;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Api.Sync;

/// <summary>
/// Every synchronizable row exposes the three fields the cursor protocol needs to place
/// it in the change stream.
/// </summary>
public interface ISyncRow
{
    Guid Id { get; }
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset? UpdatedAt { get; }
}

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
    List<SyncInventoryItemDto> InventoryItems,
    List<SyncWithdrawalPeriodDto> WithdrawalPeriods);

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
    bool IsDeleted) : ISyncRow;

public record SyncAnimalIdentifierDto(
    Guid Id,
    Guid AnimalId,
    string Type,
    string Value,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted) : ISyncRow;

public record SyncAnimalGroupDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? SpeciesId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted) : ISyncRow;

public record SyncGroupMembershipDto(
    Guid Id,
    Guid AnimalId,
    Guid GroupId,
    DateOnly JoinedAt,
    DateOnly? LeftAt,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted) : ISyncRow;

public record SyncSpeciesDto(
    Guid Id,
    string Name,
    int? GestationDays,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted) : ISyncRow;

public record SyncBreedDto(
    Guid Id,
    Guid SpeciesId,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted) : ISyncRow;

public record SyncCategoryDto(
    Guid Id,
    Guid SpeciesId,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted) : ISyncRow;

public record SyncInventoryItemDto(
    Guid Id,
    string Name,
    string Category,
    string Unit,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted) : ISyncRow;

/// <summary>
/// Active withdrawal periods (Art. 19). The field app needs these locally: without them
/// it cannot mark a cow's milk as non-sellable while offline, which is the one blocking
/// rule the milking screen has to enforce on its own.
/// </summary>
public record SyncWithdrawalPeriodDto(
    Guid Id,
    Guid AnimalId,
    Guid EventId,
    string Target,
    DateOnly StartsAt,
    DateOnly EndsAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted) : ISyncRow;

public record GetSyncPullQuery(
    string? Since = null,
    string? Collections = null,
    int BatchSize = 500) : IRequest<SyncPullResponseDto>;

public class GetSyncPullQueryHandler(
    ILivestockDbContext livestockDb,
    IInventoryDbContext inventoryDb)
    : IRequestHandler<GetSyncPullQuery, SyncPullResponseDto>
{
    public const int DefaultBatchSize = 500;
    public const int MaxBatchSize = 1000;

    public async Task<SyncPullResponseDto> Handle(GetSyncPullQuery request, CancellationToken cancellationToken)
    {
        var since = SyncCursor.Parse(request.Since);
        var limit = request.BatchSize is < 1 or > MaxBatchSize ? DefaultBatchSize : request.BatchSize;
        var requested = ParseCollections(request.Collections);
        var frontier = new CursorFrontier(since);

        var animals = await ReadAsync(
            requested, "animals", livestockDb.Animals, since, limit, frontier,
            a => new SyncAnimalDto(
                a.Id, a.Sex.ToString(), a.BirthDate, a.SpeciesId, a.BreedId, a.CategoryId,
                a.MotherId, a.FatherAnimalId, a.FatherStrawId,
                a.CreatedAt, a.UpdatedAt, a.DeletedAt != null),
            cancellationToken);

        var identifiers = await ReadAsync(
            requested, "animalIdentifiers", livestockDb.AnimalIdentifiers, since, limit, frontier,
            i => new SyncAnimalIdentifierDto(
                i.Id, i.AnimalId, i.Type.ToString(), i.Value, i.IsActive,
                i.CreatedAt, i.UpdatedAt, i.DeletedAt != null),
            cancellationToken);

        var groups = await ReadAsync(
            requested, "animalGroups", livestockDb.AnimalGroups, since, limit, frontier,
            g => new SyncAnimalGroupDto(
                g.Id, g.Name, g.Description, g.SpeciesId, g.IsActive,
                g.CreatedAt, g.UpdatedAt, g.DeletedAt != null),
            cancellationToken);

        var memberships = await ReadAsync(
            requested, "groupMemberships", livestockDb.GroupMemberships, since, limit, frontier,
            m => new SyncGroupMembershipDto(
                m.Id, m.AnimalId, m.GroupId, m.JoinedAt, m.LeftAt, m.IsActive,
                m.CreatedAt, m.UpdatedAt, m.DeletedAt != null),
            cancellationToken);

        var speciesList = await ReadAsync(
            requested, "species", livestockDb.Species, since, limit, frontier,
            s => new SyncSpeciesDto(
                s.Id, s.Name, s.GestationDays, s.CreatedAt, s.UpdatedAt, s.DeletedAt != null),
            cancellationToken);

        var breeds = await ReadAsync(
            requested, "breeds", livestockDb.Breeds, since, limit, frontier,
            b => new SyncBreedDto(
                b.Id, b.SpeciesId, b.Name, b.CreatedAt, b.UpdatedAt, b.DeletedAt != null),
            cancellationToken);

        var categories = await ReadAsync(
            requested, "animalCategories", livestockDb.AnimalCategories, since, limit, frontier,
            c => new SyncCategoryDto(
                c.Id, c.SpeciesId, c.Name, c.CreatedAt, c.UpdatedAt, c.DeletedAt != null),
            cancellationToken);

        var items = await ReadAsync(
            requested, "inventoryItems", inventoryDb.InventoryItems, since, limit, frontier,
            i => new SyncInventoryItemDto(
                i.Id, i.Name, i.Category.ToString(), i.Unit, i.Description,
                i.CreatedAt, i.UpdatedAt, i.DeletedAt != null),
            cancellationToken);

        var withdrawals = await ReadAsync(
            requested, "withdrawalPeriods", livestockDb.WithdrawalPeriods, since, limit, frontier,
            w => new SyncWithdrawalPeriodDto(
                w.Id, w.AnimalId, w.EventId, w.Target.ToString(), w.StartsAt, w.EndsAt,
                w.CreatedAt, w.UpdatedAt, w.DeletedAt != null),
            cancellationToken);

        var collections = new SyncCollectionsDto(
            animals, identifiers, groups, memberships,
            speciesList, breeds, categories, items, withdrawals);

        return new SyncPullResponseDto(frontier.Next.Format(), frontier.HasMore, collections);
    }

    private static HashSet<string>? ParseCollections(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var names = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return names.Count == 0 ? null : names;
    }

    private static async Task<List<TDto>> ReadAsync<TEntity, TDto>(
        HashSet<string>? requested,
        string name,
        IQueryable<TEntity> source,
        SyncCursor since,
        int limit,
        CursorFrontier frontier,
        Expression<Func<TEntity, TDto>> projection,
        CancellationToken cancellationToken)
        where TEntity : AuditableEntity
        where TDto : ISyncRow
    {
        if (requested is not null && !requested.Contains(name))
        {
            return [];
        }

        var sinceTimestamp = since.Timestamp;
        var sinceId = since.Id;

        // Strictly after the cursor position, ordered by that same (timestamp, id) pair:
        // the ordering is what makes the LIMIT deterministic, and a LIMIT without a
        // deterministic order silently drops rows the client will never ask for again.
        var rows = await source
            .IgnoreQueryFilters()
            .Where(e => (e.UpdatedAt ?? e.CreatedAt) > sinceTimestamp
                     || ((e.UpdatedAt ?? e.CreatedAt) == sinceTimestamp && e.Id > sinceId))
            .OrderBy(e => e.UpdatedAt ?? e.CreatedAt)
            .ThenBy(e => e.Id)
            .Take(limit)
            .Select(projection)
            .ToListAsync(cancellationToken);

        frontier.Observe(rows, truncated: rows.Count >= limit);
        return rows;
    }

    /// <summary>
    /// Combines the per-collection progress into the single cursor handed back to the
    /// client.
    ///
    /// When a collection was truncated by the batch limit, the cursor must not advance
    /// past the last row it actually delivered — otherwise the remainder of that
    /// collection falls behind the cursor and is lost forever. So the answer is the
    /// *earliest* frontier among truncated collections. Fully drained collections may be
    /// replayed from that earlier point on the next pull; the client upserts by id, so a
    /// replay costs bandwidth while a skip costs data.
    /// </summary>
    private sealed class CursorFrontier(SyncCursor since)
    {
        private SyncCursor? _earliestTruncated;
        private SyncCursor _furthestDrained = since;

        public bool HasMore { get; private set; }

        public SyncCursor Next => _earliestTruncated ?? _furthestDrained;

        public void Observe<TDto>(List<TDto> rows, bool truncated) where TDto : ISyncRow
        {
            if (rows.Count == 0)
            {
                return;
            }

            var last = rows[^1];
            var position = new SyncCursor(last.UpdatedAt ?? last.CreatedAt, last.Id);

            if (truncated)
            {
                HasMore = true;
                if (_earliestTruncated is null || position.IsBefore(_earliestTruncated.Value))
                {
                    _earliestTruncated = position;
                }

                return;
            }

            if (_furthestDrained.IsBefore(position))
            {
                _furthestDrained = position;
            }
        }
    }
}

public record GetSyncOperationsQuery(string? Status = null) : IRequest<List<SyncOperationDto>>;

public record SyncOperationDto(
    Guid Id,
    string ClientOperationId,
    string OperationType,
    string Status,
    string DeviceId,
    string? ErrorDetails,
    string? ResultRef,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt);

public class GetSyncOperationsQueryHandler(IPeopleDbContext context)
    : IRequestHandler<GetSyncOperationsQuery, List<SyncOperationDto>>
{
    public async Task<List<SyncOperationDto>> Handle(GetSyncOperationsQuery request, CancellationToken cancellationToken)
    {
        var query = context.SyncOperations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<Modules.People.Domain.SyncOperationStatus>(request.Status, true, out var parsedStatus))
        {
            query = query.Where(o => o.Status == parsedStatus);
        }

        return await query
            .OrderByDescending(o => o.ReceivedAt)
            .Take(100)
            .Select(o => new SyncOperationDto(
                o.Id,
                o.ClientOperationId.ToString(),
                o.OperationType,
                o.Status.ToString(),
                o.DeviceId ?? string.Empty,
                o.ErrorDetails,
                o.ResultRef,
                o.OccurredAt,
                o.ReceivedAt))
            .ToListAsync(cancellationToken);
    }
}
