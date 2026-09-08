using System.Linq.Expressions;
using Hato.Modules.Breeding.Application.Abstractions;
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
    List<SyncMortalityCauseDto> MortalityCauses,
    List<SyncFarmModuleDto> FarmModules,
    List<SyncAdministrationRouteDto> AdministrationRoutes,
    List<SyncTreatmentReasonDto> TreatmentReasons,
    List<SyncDoseKindDto> DoseKinds,
    List<SyncPlausibilityRangeDto> PlausibilityRanges,
    List<SyncAnimalEventDto> AnimalEvents,
    List<SyncPregnancyDto> Pregnancies,
    List<SyncBreedingServiceDto> BreedingServices);

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
    DateTimeOffset? LastEditedAt,
    DateTimeOffset? DisposedAt = null) : ISyncRow;

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
    // Nullable since 3.5a.2-B: a period anchored to a TreatmentCourse carries
    // TreatmentCourseId instead (see WithdrawalPeriod's Event-xor-Course invariant).
    // The field-app mirror table update for this new shape is 3.5a.2-C scope.
    Guid? EventId,
    Guid? TreatmentCourseId,
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

/// <summary>
/// Module on/off rows (ADR-0019). The phone already has a local table; this
/// collection is the server's view of the same data, so the toggle the operator
/// presses in the panel can land on the field without a deploy and without a
/// network round-trip at navigation time.
/// </summary>
public record SyncFarmModuleDto(
    Guid Id,
    string Key,
    bool Enabled,
    string? DisabledReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted) : ISyncRow;

/// <summary>
/// The administration routes catalog (3.5a.2-A). The field app uses these to
/// populate the "via de administración" picker when registering a treatment
/// offline — without them it cannot build a structured treatment payload
/// (docs/spec/plan-0002-fase-3-5/sub-planes/3.5a.2-A.md sec.7).
/// </summary>
public record SyncAdministrationRouteDto(
    Guid Id,
    string Key,
    string LabelEs,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted) : ISyncRow;

/// <summary>
/// The dose-form catalog (3.5a.2-B: absolute / per_weight / per_head), mirrored so
/// <c>VaccinateScreen</c> and <c>TreatScreen</c> (3.5a.2-C) can resolve a
/// <c>DoseKindId</c> for <c>createTreatmentCourse</c> offline (Art. 9) instead of
/// hardcoding the seed's stable GUIDs client-side.
/// </summary>
public record SyncDoseKindDto(
    Guid Id,
    string Key,
    string LabelEs,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted) : ISyncRow;

/// <summary>
/// The treatment reason catalog (3.5a.2-A): scheduled / curative / preventive.
/// Distinguishing them is what separates "vacuna de calendario" from "vacuna
/// porque se enfermó" on the herd's history.
/// </summary>
public record SyncTreatmentReasonDto(
    Guid Id,
    string Key,
    string LabelEs,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted) : ISyncRow;

/// <summary>
/// Plausibility range catalog (3.5a.6, ADR-0022). The field-app uses these to
/// validate weights, milk volumes and future magnitudes locally before enqueuing
/// the operation. The ranges reach the device via the pull so the validation
/// works offline (Art. 9). Bounds are decimal? because any of the four may be
/// unconfigured — the evaluator handles nulls as fail-open.
/// </summary>
public record SyncPlausibilityRangeDto(
    Guid Id,
    Guid SpeciesId,
    Guid? CategoryId,
    string Magnitude,
    decimal? PlausibleMin,
    decimal? PlausibleMax,
    decimal? AbsoluteMin,
    decimal? AbsoluteMax,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted) : ISyncRow;

/// <summary>
/// The animal/group event history (3.5a.1, ADR-0015), gated by
/// <c>livestock.animals.read</c> like the rest of the animal-support collections.
///
/// This is the deuda from BACKLOG.md ("AnimalEvent grupal aún no viaja en el pull"):
/// 3.5a.1 built the group-subject mechanism (push, domain, DB CHECK) but nothing pulled
/// it back down until 3.5a.7 needed "última vacunación, alimento del período" on the lot
/// record. One collection carries both animal- and group-subject events, mirroring the
/// same XOR the domain and the DB CHECK already enforce (ADR-0015 sec.2) — <see
/// cref="AnimalId"/> and <see cref="GroupId"/> are never both set and never both null.
/// Faking an <c>animalId</c> on a group event to keep the wire shape uniform is exactly
/// the synthetic data ADR-0015 exists to prevent, so the DTO stays honest about the
/// subject.
///
/// Events are append-only (Art. 1): a correction is a new row referencing the original
/// via <see cref="RelatedEventId"/>, never an edit. So <see cref="UpdatedAt"/> is always
/// null and <see cref="IsDeleted"/> is always false — there is nothing to overwrite or
/// tombstone, only a growing log for the cursor to walk.
/// </summary>
public record SyncAnimalEventDto(
    Guid Id,
    Guid? AnimalId,
    Guid? GroupId,
    string EventType,
    DateTimeOffset OccurredAt,
    string RecordedBy,
    Guid? RecordedById,
    string PayloadJson,
    decimal? Cost,
    Guid? RelatedEventId,
    int? AffectedCount,
    Guid? CauseId,
    Guid? RouteId,
    string? Reason,
    Guid? BatchId,
    Guid? HealthPlanItemId,
    Guid? AppliedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted) : ISyncRow;

public record SyncPregnancyDto(
    Guid Id,
    Guid DamId,
    Guid? ServiceId,
    string Status,
    DateOnly ExpectedBirthDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted) : ISyncRow;

public record SyncBreedingServiceDto(
    Guid Id,
    Guid DamId,
    string ServiceType,
    Guid? SireAnimalId,
    Guid? StrawId,
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
    IPeopleDbContext peopleDb,
    IBreedingDbContext breedingDb,
    IUserPermissionsReader permissionsReader,
    ICurrentUser currentUser)
    : IRequestHandler<GetSyncPullQuery, SyncPullResponseDto>
{
    public const int DefaultBatchSize = 500;
    public const int MaxBatchSize = 1000;

    /// <summary>
    /// The permission a role needs to read each collection (docs/spec/plan-0001-fase-3/spec.md sec.3.A, pull task
    /// 4: "el empleado solo baja lo que le corresponde"). Reference tables (species,
    /// breeds, categories) sit under the same permission as animals: they exist to
    /// support working with animals, so a role with no livestock access has no use for
    /// them either. The farm_modules list sits behind the Settings read bit so the
    /// visibility check is consistent with the panel-toggle screen.
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
            ["farmModules"] = SystemPermissions.SettingsFarmModulesRead,
            ["administrationRoutes"] = SystemPermissions.LivestockAnimalsRead,
            ["treatmentReasons"] = SystemPermissions.LivestockAnimalsRead,
            ["doseKinds"] = SystemPermissions.LivestockAnimalsRead,
            ["plausibilityRanges"] = SystemPermissions.LivestockAnimalsRead,
            ["animalEvents"] = SystemPermissions.LivestockAnimalsRead,
            ["pregnancies"] = SystemPermissions.BreedingEventsRead,
            ["breedingServices"] = SystemPermissions.BreedingEventsRead,
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
                a.CreatedAt, a.UpdatedAt, a.DeletedAt != null, a.LastEditedAt,
                a.DisposedAt),
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
                w.Id, w.AnimalId, w.EventId, w.TreatmentCourseId, w.Target.ToString(), w.StartsAt, w.EndsAt,
                w.CreatedAt, w.UpdatedAt, w.DeletedAt != null),
            cancellationToken);

        var mortalityCauses = await ReadAsync(
            effective, "mortalityCauses", livestockDb.MortalityCauses, since, limit, frontier,
            c => new SyncMortalityCauseDto(
                c.Id, c.Name, c.IsActive, c.CreatedAt, c.UpdatedAt, c.DeletedAt != null),
            cancellationToken);

        var farmModules = await ReadAsync(
            effective, "farmModules", peopleDb.FarmModules, since, limit, frontier,
            m => new SyncFarmModuleDto(
                m.Id, m.Key, m.Enabled, m.DisabledReason, m.CreatedAt, m.UpdatedAt, false),
            cancellationToken);

        var administrationRoutes = await ReadAsync(
            effective, "administrationRoutes", livestockDb.AdministrationRoutes, since, limit, frontier,
            r => new SyncAdministrationRouteDto(
                r.Id, r.Key, r.LabelEs, r.IsActive, r.CreatedAt, r.UpdatedAt, r.DeletedAt != null),
            cancellationToken);

        var treatmentReasons = await ReadAsync(
            effective, "treatmentReasons", livestockDb.TreatmentReasons, since, limit, frontier,
            r => new SyncTreatmentReasonDto(
                r.Id, r.Key, r.LabelEs, r.IsActive, r.CreatedAt, r.UpdatedAt, r.DeletedAt != null),
            cancellationToken);

        var doseKinds = await ReadAsync(
            effective, "doseKinds", livestockDb.DoseKinds, since, limit, frontier,
            k => new SyncDoseKindDto(
                k.Id, k.Key, k.LabelEs, k.IsActive, k.CreatedAt, k.UpdatedAt, k.DeletedAt != null),
            cancellationToken);

        var plausibilityRanges = await ReadAsync(
            effective, "plausibilityRanges", livestockDb.PlausibilityRanges, since, limit, frontier,
            r => new SyncPlausibilityRangeDto(
                r.Id, r.SpeciesId, r.CategoryId, r.Magnitude,
                r.PlausibleMin, r.PlausibleMax, r.AbsoluteMin, r.AbsoluteMax,
                r.IsActive, r.CreatedAt, r.UpdatedAt, r.DeletedAt != null),
            cancellationToken);

        var animalEvents = await ReadAsync(
            effective, "animalEvents", livestockDb.AnimalEvents, since, limit, frontier,
            e => new SyncAnimalEventDto(
                e.Id, e.AnimalId, e.GroupId, e.EventType.ToString(), e.OccurredAt,
                e.RecordedByLabel, e.RecordedById, e.PayloadJson, e.Cost, e.RelatedEventId,
                e.AffectedCount, e.CauseId, e.RouteId, e.Reason, e.BatchId,
                e.HealthPlanItemId, e.AppliedByUserId,
                e.CreatedAt, e.UpdatedAt, e.DeletedAt != null),
            cancellationToken);

        var pregnancies = await ReadAsync(
            effective, "pregnancies", breedingDb.Pregnancies, since, limit, frontier,
            p => new SyncPregnancyDto(
                p.Id, p.DamId, p.ServiceId, p.Status.ToString(), p.ExpectedBirthDate,
                p.CreatedAt, p.UpdatedAt, p.DeletedAt != null),
            cancellationToken);

        var breedingServices = await ReadAsync(
            effective, "breedingServices", breedingDb.BreedingServices, since, limit, frontier,
            s => new SyncBreedingServiceDto(
                s.Id, s.DamId, s.ServiceType.ToString(), s.SireAnimalId, s.StrawId,
                s.CreatedAt, s.UpdatedAt, s.DeletedAt != null),
            cancellationToken);

        var collections = new SyncCollectionsDto(
            animals, identifiers, groups, memberships,
            speciesList, breeds, categories, items, withdrawals, mortalityCauses, farmModules,
            administrationRoutes, treatmentReasons, doseKinds,
            plausibilityRanges, animalEvents,
            pregnancies, breedingServices);

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

/// <summary>
/// Scopes sync operations to the caller's own user unless they hold people.users.manage,
/// which grants global supervision across all users (D3).
/// </summary>
public class GetSyncOperationsQueryHandler(
    IPeopleDbContext context,
    ICurrentUser currentUser,
    IUserPermissionsReader permissionsReader)
    : IRequestHandler<GetSyncOperationsQuery, List<SyncOperationDto>>
{
    public async Task<List<SyncOperationDto>> Handle(GetSyncOperationsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("La consulta de operaciones requiere un usuario autenticado.");

        var permissions = await permissionsReader.GetPermissionCodesAsync(userId, cancellationToken);
        var canManageUsers = permissions.Contains(SystemPermissions.PeopleUsersManage);

        var query = context.SyncOperations.AsNoTracking();

        if (!canManageUsers)
        {
            query = query.Where(o => o.UserId == userId);
        }

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
