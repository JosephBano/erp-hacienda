using System.Linq.Expressions;
using Hato.Modules.Inventory.Application.Abstractions;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Contracts;
using Hato.Modules.People.Domain;
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
    List<SyncWithdrawalPeriodDto> WithdrawalPeriods,
    List<SyncMortalityCauseDto> MortalityCauses);

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
    bool IsDeleted,
    // The LWW baseline (ADR-0008): a device echoes this back as knownUpdatedAt on its
    // next updateAnimal push, so the server can tell whether another write landed on
    // the row after this device last saw it. Distinct from UpdatedAt, which is server
    // processing time and would make conflict resolution depend on network luck.
    DateTimeOffset? LastEditedAt) : ISyncRow;

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
    string TrackingMode,
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
    bool IsMilkable,
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

/// <summary>
/// The mortality causes catalog (Art. 8, 3.5a.3), so "baja con causa" can offer the list
/// offline instead of blocking on a round trip the field may not have.
/// </summary>
public record SyncMortalityCauseDto(
    Guid Id,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted) : ISyncRow;

public record GetSyncPullQuery(
    string? Since = null,
    string? Collections = null,
    int BatchSize = 500) : IRequest<SyncPullResponseDto>;

public class GetSyncPullQueryHandler(
    ILivestockDbContext livestockDb,
    IInventoryDbContext inventoryDb,
    IUserPermissionsReader permissionsReader,
    ICurrentUser currentUser)
    : IRequestHandler<GetSyncPullQuery, SyncPullResponseDto>
{
    public const int DefaultBatchSize = 500;
    public const int MaxBatchSize = 1000;

    /// <summary>
    /// The permission a role needs to read each collection (PLAN-FASE-3-4 sec.3.A, pull task
    /// 4: "el empleado solo baja lo que le corresponde"). Reference tables (species,
    /// breeds, categories) sit under the same permission as animals: they exist to
    /// support working with animals, so a role with no livestock access has no use for
    /// them either.
    /// </summary>
    private static readonly Dictionary<string, string> RequiredPermissionByCollection =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["animals"] = SystemPermissions.LivestockAnimalsRead,
            ["animalIdentifiers"] = SystemPermissions.LivestockAnimalsRead,
            ["animalGroups"] = SystemPermissions.LivestockAnimalsRead,
            ["groupMemberships"] = SystemPermissions.LivestockAnimalsRead,
            ["species"] = SystemPermissions.LivestockAnimalsRead,
            ["breeds"] = SystemPermissions.LivestockAnimalsRead,
            ["animalCategories"] = SystemPermissions.LivestockAnimalsRead,
            ["withdrawalPeriods"] = SystemPermissions.LivestockAnimalsRead,
            ["inventoryItems"] = SystemPermissions.InventoryItemsRead,
            ["mortalityCauses"] = SystemPermissions.LivestockAnimalsRead,
        };

    public async Task<SyncPullResponseDto> Handle(GetSyncPullQuery request, CancellationToken cancellationToken)
    {
        var since = SyncCursor.Parse(request.Since);
        var limit = request.BatchSize is < 1 or > MaxBatchSize ? DefaultBatchSize : request.BatchSize;
        var requested = ParseCollections(request.Collections);
        var frontier = new CursorFrontier(since);

        // Deny by default: a user with no resolvable id (should not happen behind
        // RequireAuthorization, but the pull must never fail open) sees nothing.
        var permissionCodes = currentUser.UserId is { } userId
            ? await permissionsReader.GetPermissionCodesAsync(userId, cancellationToken)
            : [];

        var visible = RequiredPermissionByCollection
            .Where(kv => permissionCodes.Contains(kv.Value))
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // The explicit `collections=` filter and the permission filter both apply: asking
        // by name for a collection the role cannot read must not leak it.
        var effective = requested is null
            ? visible
            : requested.Where(visible.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var animals = await ReadAsync(
            effective, "animals", livestockDb.Animals, since, limit, frontier,
            a => new SyncAnimalDto(
                a.Id, a.Sex.ToString(), a.BirthDate, a.SpeciesId, a.BreedId, a.CategoryId,
                a.MotherId, a.FatherAnimalId, a.FatherStrawId,
                a.CreatedAt, a.UpdatedAt, a.DeletedAt != null, a.LastEditedAt),
            cancellationToken);

        var identifiers = await ReadAsync(
            effective, "animalIdentifiers", livestockDb.AnimalIdentifiers, since, limit, frontier,
            i => new SyncAnimalIdentifierDto(
                i.Id, i.AnimalId, i.Type.ToString(), i.Value, i.IsActive,
                i.CreatedAt, i.UpdatedAt, i.DeletedAt != null),
            cancellationToken);

        var groups = await ReadAsync(
            effective, "animalGroups", livestockDb.AnimalGroups, since, limit, frontier,
            g => new SyncAnimalGroupDto(
                g.Id, g.Name, g.Description, g.SpeciesId, g.IsActive, g.TrackingMode.ToString(),
                g.CreatedAt, g.UpdatedAt, g.DeletedAt != null),
            cancellationToken);

        var memberships = await ReadAsync(
            effective, "groupMemberships", livestockDb.GroupMemberships, since, limit, frontier,
            m => new SyncGroupMembershipDto(
                m.Id, m.AnimalId, m.GroupId, m.JoinedAt, m.LeftAt, m.IsActive,
                m.CreatedAt, m.UpdatedAt, m.DeletedAt != null),
            cancellationToken);

        var speciesList = await ReadAsync(
            effective, "species", livestockDb.Species, since, limit, frontier,
            s => new SyncSpeciesDto(
                s.Id, s.Name, s.GestationDays, s.IsMilkable, s.CreatedAt, s.UpdatedAt, s.DeletedAt != null),
            cancellationToken);

        var breeds = await ReadAsync(
            effective, "breeds", livestockDb.Breeds, since, limit, frontier,
            b => new SyncBreedDto(
                b.Id, b.SpeciesId, b.Name, b.CreatedAt, b.UpdatedAt, b.DeletedAt != null),
            cancellationToken);

        var categories = await ReadAsync(
            effective, "animalCategories", livestockDb.AnimalCategories, since, limit, frontier,
            c => new SyncCategoryDto(
                c.Id, c.SpeciesId, c.Name, c.CreatedAt, c.UpdatedAt, c.DeletedAt != null),
            cancellationToken);

        var items = await ReadAsync(
            effective, "inventoryItems", inventoryDb.InventoryItems, since, limit, frontier,
            i => new SyncInventoryItemDto(
                i.Id, i.Name, i.Category.ToString(), i.Unit, i.Description,
                i.CreatedAt, i.UpdatedAt, i.DeletedAt != null),
            cancellationToken);

        var withdrawals = await ReadAsync(
            effective, "withdrawalPeriods", livestockDb.WithdrawalPeriods, since, limit, frontier,
            w => new SyncWithdrawalPeriodDto(
                w.Id, w.AnimalId, w.EventId, w.Target.ToString(), w.StartsAt, w.EndsAt,
                w.CreatedAt, w.UpdatedAt, w.DeletedAt != null),
            cancellationToken);

        var mortalityCauses = await ReadAsync(
            effective, "mortalityCauses", livestockDb.MortalityCauses, since, limit, frontier,
            c => new SyncMortalityCauseDto(
                c.Id, c.Name, c.IsActive, c.CreatedAt, c.UpdatedAt, c.DeletedAt != null),
            cancellationToken);

        var collections = new SyncCollectionsDto(
            animals, identifiers, groups, memberships,
            speciesList, breeds, categories, items, withdrawals, mortalityCauses);

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

/// <summary>The LWW conflict tray (ADR-0008): what an admin reviews to see fields two devices fought over.</summary>
public record GetSyncConflictsQuery(string? EntityType = null) : IRequest<List<SyncConflictDto>>;

public record SyncConflictDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string FieldName,
    string? ServerValue,
    string? AttemptedValue,
    string Resolution,
    string? DeviceId,
    DateTimeOffset DetectedAt);

public class GetSyncConflictsQueryHandler(IPeopleDbContext context)
    : IRequestHandler<GetSyncConflictsQuery, List<SyncConflictDto>>
{
    public async Task<List<SyncConflictDto>> Handle(GetSyncConflictsQuery request, CancellationToken cancellationToken)
    {
        var query = context.SyncConflicts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            query = query.Where(c => c.EntityType == request.EntityType);
        }

        return await query
            .OrderByDescending(c => c.DetectedAt)
            .Take(100)
            .Select(c => new SyncConflictDto(
                c.Id,
                c.EntityType,
                c.EntityId,
                c.FieldName,
                c.ServerValue,
                c.AttemptedValue,
                c.Resolution,
                c.DeviceId,
                c.DetectedAt))
            .ToListAsync(cancellationToken);
    }
}
