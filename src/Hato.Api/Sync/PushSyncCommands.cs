using System.Text.Json;
using System.Text.Json.Serialization;
using Hato.Modules.Breeding.Application.Birthings;
using Hato.Modules.Livestock.Application.AnimalGroups;
using Hato.Modules.Livestock.Application.Animals;
using Hato.Modules.Livestock.Application.Events;
using Hato.Modules.People.Application.Abstractions;
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
    ISender sender)
    : IRequestHandler<PushSyncBatchCommand, PushSyncBatchResponseDto>
{
    /// <summary>
    /// Upper bound on operations per request. A device that has been offline for a week
    /// splits its outbox into batches of this size rather than opening one enormous
    /// transaction that times out halfway.
    /// </summary>
    public const int MaxBatchSize = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
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

        var results = new List<SyncOperationResultDto>(request.Operations.Count);

        foreach (var operation in request.Operations)
        {
            results.Add(await ProcessAsync(operation, request.DeviceId, userId, cancellationToken));
        }

        return new PushSyncBatchResponseDto(results.Count, results);
    }

    private async Task<SyncOperationResultDto> ProcessAsync(
        SyncPushOperationDto operation,
        string? deviceId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var payloadJson = operation.Payload.ValueKind == JsonValueKind.Undefined
            ? "{}"
            : operation.Payload.GetRawText();

        var claim = await ClaimAsync(operation, deviceId, userId, payloadJson, cancellationToken);

        if (claim.Existing is not null)
        {
            return new SyncOperationResultDto(
                operation.ClientOperationId,
                nameof(SyncOperationStatus.Duplicate),
                claim.Existing.ResultRef,
                claim.Existing.ErrorDetails);
        }

        var record = claim.Claimed!;

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
    /// flow means adding a case here — and a push test for it (PLAN-FASE-3-4 §2.2).
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
