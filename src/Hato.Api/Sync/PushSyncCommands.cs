using System.Text.Json;
using System.Text.Json.Serialization;
using Hato.Modules.Breeding.Application.Birthings;
using Hato.Modules.Inventory.Application.Consumptions;
using Hato.Modules.Livestock.Application.AnimalGroups;
using Hato.Modules.Livestock.Application.Animals;
using Hato.Modules.Livestock.Application.Events;
using Hato.Modules.Livestock.Application.TreatmentCourses;
using Hato.Modules.Livestock.Domain;
using Hato.Modules.People.Application.Abstractions;
using Hato.Modules.People.Contracts;
using Hato.Modules.People.Domain;
using Hato.Modules.Production.Application.Milking;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Api.Sync;

public record SyncPushOperationDto(
    Guid ClientOperationId,
    string OperationType,
    DateTimeOffset OccurredAt,
    JsonElement Payload);

public record PushSyncBatchCommand(
    string? DeviceId,
    List<SyncPushOperationDto> Operations) : IRequest<PushSyncBatchResponseDto>;

public record SyncOperationResultDto(
    Guid ClientOperationId,
    string Status,
    string? ResultRef,
    string? ErrorDetails);

public record PushSyncBatchResponseDto(
    int ProcessedCount,
    List<SyncOperationResultDto> Results);

/// <summary>
/// Server side of the offline push protocol (ADR-0008).
///
/// Two invariants govern everything here. **Never duplicate**: the same
/// <c>clientOperationId</c> replayed any number of times — by a double tap, a retry after
/// a dropped connection, or two devices sharing a queue — must produce exactly one
/// business record. **Never lose silently**: an operation the server refuses is written
/// down with its reason and handed back, so it can surface in the problems tray instead
/// of evaporating.
///
/// The push is deliberately *not* a back door: every operation is dispatched through the
/// very same MediatR command the web API uses, so validation, domain invariants and the
/// withdrawal-period block (Art. 19) apply identically.
/// </summary>
public class PushSyncBatchCommandHandler(
    IPeopleDbContext peopleDb,
    ICurrentUser currentUser,
    ISender sender,
    IUserPermissionsReader permissionsReader)
    : IRequestHandler<PushSyncBatchCommand, PushSyncBatchResponseDto>
{
    /// <summary>
    /// Upper bound on operations per request. A device that has been offline for a week
    /// splits its outbox into batches of this size rather than opening one enormous
    /// transaction that times out halfway.
    /// </summary>
    public const int MaxBatchSize = 500;

    /// <summary>
    /// Permissions required to execute each push operation type.
    /// Every push operation must have an explicit mapping. Operations not listed here are rejected.
    /// </summary>
    public static readonly Dictionary<string, string> RequiredPermissionByOperation =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["recordMilking"] = SystemPermissions.ProductionMilkingRecord,
            ["recordAnimalEvent"] = SystemPermissions.LivestockAnimalsWrite,
            ["createTreatmentCourse"] = SystemPermissions.LivestockAnimalsWrite,
            ["recordGroupEvent"] = SystemPermissions.LivestockAnimalsWrite,
            ["recordFeedConsumption"] = SystemPermissions.InventoryFeedConsumptionsRecord,
            ["createAnimal"] = SystemPermissions.LivestockAnimalsWrite,
            ["recordBirth"] = SystemPermissions.BreedingEventsRecord,
            ["moveAnimal"] = SystemPermissions.LivestockAnimalsWrite,
            ["updateAnimal"] = SystemPermissions.LivestockAnimalsWrite,
            ["recordCorrection"] = SystemPermissions.LivestockAnimalsWrite,
            ["assignAnimalIdentifier"] = SystemPermissions.LivestockAnimalsWrite,
        };

    // UnmappedMemberHandling.Disallow closes the exact hole 3.5a.2-C was written to
    // fix (see docs/spec/plan-0002-fase-3-5/sub-planes/3.5a.2-C.md): before this, an operation with a
    // field the target command does not declare — `reasonId` instead of `reason`,
    // a stray `doseKg` — was silently dropped and the push still answered
    // "Accepted", because `Deserialize<T>` just ignored what it did not recognise.
    // That is precisely the failure mode that produced a false-positive close on an
    // earlier phase (a dropped `motherId`, no error, no signal). Disallow turns an
    // unknown field into a loud `JsonException` → 400 the device's problems tray can
    // show, instead of a record silently missing data on the server.
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public async Task<PushSyncBatchResponseDto> Handle(
        PushSyncBatchCommand request, CancellationToken cancellationToken)
    {
        if (request.Operations.Count > MaxBatchSize)
        {
            throw new DomainException(
                $"El lote trae {request.Operations.Count} operaciones y el máximo es {MaxBatchSize}. " +
                "Divida el envío: un lote rechazado entero obligaría al dispositivo a reintentar todo.");
        }

        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("La sincronización requiere un usuario autenticado.");

        var userPermissions = await permissionsReader.GetPermissionCodesAsync(userId, cancellationToken);
        var isAdmin = await peopleDb.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.User.IsActive && ur.Role != null)
            .AnyAsync(ur => ur.Role.Code == SystemRoles.Admin, cancellationToken);

        var results = new List<SyncOperationResultDto>(request.Operations.Count);

        foreach (var operation in request.Operations)
        {
            results.Add(await ProcessAsync(operation, request.DeviceId, userId, userPermissions, isAdmin, cancellationToken));
        }

        return new PushSyncBatchResponseDto(results.Count, results);
    }

    private async Task<SyncOperationResultDto> ProcessAsync(
        SyncPushOperationDto operation,
        string? deviceId,
        Guid userId,
        HashSet<string> userPermissions,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var payloadJson = operation.Payload.ValueKind == JsonValueKind.Undefined
            ? "{}"
            : operation.Payload.GetRawText();

        var claim = await ClaimAsync(operation, deviceId, userId, payloadJson, cancellationToken);

        if (claim.Existing is not null)
        {
            var errorDetails = claim.Existing.Status switch
            {
                SyncOperationStatus.Rejected => claim.Existing.ErrorDetails ?? "La operación fue rechazada previamente en el servidor.",
                SyncOperationStatus.Processing => "La operación anterior sigue en procesamiento en el servidor.",
                _ => claim.Existing.ErrorDetails
            };

            return new SyncOperationResultDto(
                operation.ClientOperationId,
                nameof(SyncOperationStatus.Duplicate),
                claim.Existing.ResultRef,
                errorDetails);
        }

        var record = claim.Claimed!;

        if (!RequiredPermissionByOperation.TryGetValue(operation.OperationType, out var requiredPermission))
        {
            var reason = $"Tipo de operación no soportado: '{operation.OperationType}'.";
            record.MarkRejected(reason);
            await peopleDb.SaveChangesAsync(cancellationToken);

            return new SyncOperationResultDto(
                operation.ClientOperationId, nameof(SyncOperationStatus.Rejected), null, reason);
        }

        if (!isAdmin && !userPermissions.Contains(requiredPermission))
        {
            var reason = $"No tiene el permiso requerido '{requiredPermission}' para ejecutar '{operation.OperationType}'.";
            record.MarkRejected(reason);
            await peopleDb.SaveChangesAsync(cancellationToken);

            return new SyncOperationResultDto(
                operation.ClientOperationId, nameof(SyncOperationStatus.Rejected), null, reason);
        }

        try
        {
            var resultRef = await ExecuteAsync(operation, payloadJson, deviceId, cancellationToken);
            record.MarkAccepted(resultRef);
            await peopleDb.SaveChangesAsync(cancellationToken);

            return new SyncOperationResultDto(
                operation.ClientOperationId, nameof(SyncOperationStatus.Accepted), resultRef, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var reason = Describe(exception);
            record.MarkRejected(reason);
            await peopleDb.SaveChangesAsync(cancellationToken);

            return new SyncOperationResultDto(
                operation.ClientOperationId, nameof(SyncOperationStatus.Rejected), null, reason);
        }
    }

    /// <summary>
    /// Reserves the operation id before doing any work. The unique index on
    /// <c>client_operation_id</c> is the actual arbiter: a plain "select then insert"
    /// leaves a window in which two concurrent pushes both see nothing and both insert.
    /// </summary>
    private async Task<(SyncOperation? Claimed, SyncOperation? Existing)> ClaimAsync(
        SyncPushOperationDto operation,
        string? deviceId,
        Guid userId,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var existing = await peopleDb.SyncOperations
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ClientOperationId == operation.ClientOperationId, cancellationToken);

        if (existing is not null)
        {
            return (null, existing);
        }

        var record = new SyncOperation(
            operation.ClientOperationId,
            userId,
            deviceId,
            operation.OperationType,
            payloadJson,
            operation.OccurredAt,
            DateTimeOffset.UtcNow);

        peopleDb.SyncOperations.Add(record);

        try
        {
            await peopleDb.SaveChangesAsync(cancellationToken);
            return (record, null);
        }
        catch (DbUpdateException)
        {
            // Lost the race: another request claimed this id between the SELECT and the
            // INSERT. Detach first — an entity stuck in Added state would make every
            // later SaveChanges in this batch fail with the same violation.
            peopleDb.Entry(record).State = EntityState.Detached;

            var winner = await peopleDb.SyncOperations
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ClientOperationId == operation.ClientOperationId, cancellationToken);

            return (null, winner);
        }
    }

    /// <summary>
    /// Routes an operation to the same command the web API would use. Adding a new field
    /// flow means adding a case here — and a push test for it (docs/spec/plan-0001-fase-3/spec.md sec.2.2).
    /// </summary>
    private async Task<string?> ExecuteAsync(
        SyncPushOperationDto operation, string payloadJson, string? deviceId, CancellationToken cancellationToken)
    {
        switch (operation.OperationType.ToLowerInvariant())
        {
            case "recordmilking":
                {
                    var command = Deserialize<RecordMilkingSessionCommand>(payloadJson, "ordeño");
                    var id = await sender.Send(command, cancellationToken);
                    return id.ToString();
                }

            case "recordanimalevent":
                {
                    var command = Deserialize<RecordAnimalEventCommand>(payloadJson, "evento");
                    var id = await sender.Send(command, cancellationToken);
                    return id.ToString();
                }

            case "createtreatmentcourse":
                {
                    // 3.5a.2-C: VaccinateScreen and TreatScreen push a real
                    // TreatmentCourse instead of overloading recordAnimalEvent's
                    // legacy free-text dose migration (that path exists purely for
                    // backward compat, see RecordAnimalEventCommand). The command's
                    // field names ARE the wire contract; deserializing straight into
                    // it (rather than through a separate payload record) is what
                    // keeps the mobile payload and the accepted shape from drifting
                    // apart silently — the exact defect this sub-branch exists to
                    // close (an unmapped field discarded without a trace).
                    var command = Deserialize<CreateTreatmentCourseCommand>(payloadJson, "serie de tratamiento");
                    var id = await sender.Send(command, cancellationToken);
                    return id.ToString();
                }

            case "recordgroupevent":
                {
                    var command = Deserialize<RecordGroupEventCommand>(payloadJson, "evento de lote");
                    var id = await sender.Send(command, cancellationToken);
                    return id.ToString();
                }

            case "recordfeedconsumption":
                {
                    // 3.5a.7 task 5: "el lote comió N sacos" is not an AnimalEvent — it is
                    // an Inventory-module write (GroupFeedConsumption) so the batch
                    // decrements and the cost-prorate engine reads kilograms regardless of
                    // what unit the operator typed ("bug del saco",
                    // docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.5). Routed to the same command the
                    // POST /api/v1/inventory/feed-consumptions endpoint uses.
                    var payload = Deserialize<RecordFeedConsumptionPushPayload>(payloadJson, "consumo de alimento");
                    var consumedAt = payload.ConsumedAt ?? DateOnly.FromDateTime(operation.OccurredAt.UtcDateTime);
                    var command = new RecordGroupFeedConsumptionCommand(
                        payload.GroupId,
                        payload.InventoryItemId,
                        payload.Quantity,
                        consumedAt,
                        payload.RecordedBy,
                        payload.Unit,
                        payload.BatchId,
                        payload.Notes);
                    var id = await sender.Send(command, cancellationToken);
                    return id.ToString();
                }

            case "createanimal":
                {
                    var command = Deserialize<RegisterAnimalCommand>(payloadJson, "animal");
                    var id = await sender.Send(command, cancellationToken);
                    return id.ToString();
                }

            case "recordbirth":
                {
                    // Routed to Breeding's own command so the calf is enrolled through
                    // IAnimalRegistrationService with its genealogy set. Registering the
                    // calf as a plain animal instead would silently drop mother and
                    // father: RegisterAnimalCommand has no genealogy fields at all.
                    var command = Deserialize<RecordBirthingCommand>(payloadJson, "parto");
                    var birthing = await sender.Send(command, cancellationToken);
                    return birthing.Id.ToString();
                }

            case "moveanimal":
                {
                    var move = Deserialize<MoveAnimalPayload>(payloadJson, "movimiento");
                    if (move.FromGroupId.HasValue && move.FromGroupId.Value == move.ToGroupId)
                    {
                        throw new DomainException("El lote de destino debe ser diferente del lote de origen.");
                    }

                    var movedOn = move.MovedOn ?? DateOnly.FromDateTime(DateTime.UtcNow);

                    if (move.FromGroupId is { } from)
                    {
                        await sender.Send(new RemoveGroupMemberCommand(from, move.AnimalId, movedOn), cancellationToken);
                    }

                    await sender.Send(new AddGroupMemberCommand(move.ToGroupId, move.AnimalId, movedOn), cancellationToken);
                    return move.ToGroupId.ToString();
                }

            case "updateanimal":
                {
                    // The one editable-entity flow (ADR-0008): two devices editing the
                    // same animal offline resolve by LWW, and every genuinely conflicting
                    // field is written to the conflict log for the panel to review.
                    var payload = Deserialize<UpdateAnimalPushPayload>(payloadJson, "actualización de animal");
                    var command = new UpdateAnimalCommand(
                        payload.AnimalId,
                        payload.BreedId,
                        payload.CategoryId,
                        payload.BirthDate,
                        operation.OccurredAt,
                        CheckForConflicts: true,
                        KnownUpdatedAt: payload.KnownUpdatedAt,
                        ClientOperationId: operation.ClientOperationId,
                        DeviceId: deviceId);

                    var result = await sender.Send(command, cancellationToken);
                    LogConflicts(result, operation.ClientOperationId, deviceId);
                    return result.AnimalId.ToString();
                }

            case "recordcorrection":
                {
                    // Field correction flow (docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.8, ADR-0017).
                    // The original event must already exist on the server and the
                    // correction must arrive on the same calendar day; the handler checks
                    // both. The push op is the always-routed path so the outbox can
                    // stay dumb — it only knows the operation type.
                    var payload = Deserialize<RecordCorrectionPushPayload>(payloadJson, "corrección");
                    var command = new RecordCorrectionCommand(
                        payload.OriginalEventId,
                        operation.OccurredAt,
                        payload.RecordedBy,
                        payload.Reason);
                    var id = await sender.Send(command, cancellationToken);
                    return id.ToString();
                }

            case "assignanimalidentifier":
                {
                    var payload = Deserialize<AssignAnimalIdentifierPushPayload>(payloadJson, "asignación de identificación");
                    var validFrom = payload.ValidFrom ?? DateOnly.FromDateTime(operation.OccurredAt.UtcDateTime);
                    var command = new AssignAnimalIdentifierCommand(
                        payload.AnimalId,
                        payload.Type,
                        payload.Value,
                        validFrom);
                    var id = await sender.Send(command, cancellationToken);
                    return id.ToString();
                }

            default:
                throw new DomainException($"Tipo de operación no soportado: '{operation.OperationType}'.");
        }
    }

    /// <summary>
    /// Writes one <see cref="SyncConflict"/> row per field that a genuine conflict window
    /// touched. A record with nothing to reconcile (no offline window, or the incoming
    /// values already matched what was stored) produces none.
    /// </summary>
    private void LogConflicts(UpdateAnimalResult result, Guid clientOperationId, string? deviceId)
    {
        if (result.Conflicts.Count == 0) return;

        var resolution = result.ClientChangesApplied
            ? SyncConflictResolution.ClientWon
            : SyncConflictResolution.ServerWon;

        foreach (var conflict in result.Conflicts)
        {
            peopleDb.SyncConflicts.Add(SyncConflict.Create(
                "Animal",
                result.AnimalId,
                conflict.FieldName,
                conflict.ServerValue,
                conflict.AttemptedValue,
                resolution,
                clientOperationId,
                deviceId));
        }
    }

    private static T Deserialize<T>(string payloadJson, string label)
    {
        T? command;

        try
        {
            command = JsonSerializer.Deserialize<T>(payloadJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new DomainException($"Payload de {label} ilegible: {exception.Message}");
        }

        return command ?? throw new DomainException($"Payload de {label} inválido.");
    }

    private static string Describe(Exception exception) =>
        exception.InnerException?.Message ?? exception.Message;
}

/// <summary>
/// A move is "leave the old lot, join the new one". <c>FromGroupId</c> is optional: an
/// animal can be entering a lot for the first time.
/// </summary>
public record MoveAnimalPayload(
    Guid AnimalId,
    Guid ToGroupId,
    Guid? FromGroupId = null,
    DateOnly? MovedOn = null);

/// <summary>
/// <paramref name="KnownUpdatedAt"/> is the animal's edit moment
/// (<see cref="Hato.Modules.Livestock.Domain.Animal.LastEditedAt"/>) as this device last
/// saw it, normally read straight off its last pull of the row. Omitting it declares "this
/// was never an offline edit" — an admin panel writing synchronously has no stale-read
/// window to report.
/// </summary>
public record UpdateAnimalPushPayload(
    Guid AnimalId,
    Guid? BreedId,
    Guid? CategoryId,
    DateOnly? BirthDate,
    DateTimeOffset? KnownUpdatedAt = null);

/// <summary>
/// Field correction payload (3.5a.8). The original event id is the
/// <c>resultRef</c> the device got back when it pushed the original op, so the
/// server can resolve the correction without an extra round trip. The reason is
/// the operator's free-text explanation; the server stores it under the event
/// payload as JSONB.
/// </summary>
public record RecordCorrectionPushPayload(
    Guid OriginalEventId,
    string RecordedBy,
    string Reason);

/// <summary>
/// 3.5a.7 task 5 push payload. Field names mirror <see cref="RecordGroupFeedConsumptionCommand"/>
/// exactly (case-insensitively, per <c>JsonOptions</c> above) so a rename on either side is
/// caught by <c>SyncPushFeedConsumptionTests</c> instead of being silently dropped — the
/// same class of bug that closed the pilot in false in Fase 3, because this deserializer has
/// no <c>UnmappedMemberHandling.Disallow</c>. <paramref name="ConsumedAt"/> is optional: the
/// phone may omit it and mean "the day this was recorded", exactly like <c>MoveAnimalPayload.MovedOn</c>.
/// </summary>
public record RecordFeedConsumptionPushPayload(
    Guid GroupId,
    Guid InventoryItemId,
    decimal Quantity,
    string RecordedBy,
    string? Unit = null,
    Guid? BatchId = null,
    string? Notes = null,
    DateOnly? ConsumedAt = null);

public record AssignAnimalIdentifierPushPayload(
    Guid AnimalId,
    IdentifierType Type,
    string Value,
    DateOnly? ValidFrom = null);
