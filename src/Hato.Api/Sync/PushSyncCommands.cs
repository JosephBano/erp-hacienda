using System.Text.Json;
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

public class PushSyncBatchCommandHandler(
    IPeopleDbContext peopleDb,
    ICurrentUser currentUser,
    ISender sender)
    : IRequestHandler<PushSyncBatchCommand, PushSyncBatchResponseDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PushSyncBatchResponseDto> Handle(PushSyncBatchCommand request, CancellationToken cancellationToken)
    {
        var results = new List<SyncOperationResultDto>();
        var userId = currentUser.UserId ?? Guid.Empty;

        foreach (var op in request.Operations)
        {
            // 1. Check idempotency
            var existing = await peopleDb.SyncOperations
                .FirstOrDefaultAsync(s => s.ClientOperationId == op.ClientOperationId, cancellationToken);

            if (existing is not null)
            {
                results.Add(new SyncOperationResultDto(
                    op.ClientOperationId,
                    "Duplicate",
                    existing.ResultRef,
                    existing.ErrorDetails));
                continue;
            }

            var rawJson = op.Payload.ValueKind == JsonValueKind.Undefined ? "{}" : op.Payload.GetRawText();

            var syncOp = new SyncOperation(
                op.ClientOperationId,
                userId,
                request.DeviceId,
                op.OperationType,
                rawJson,
                op.OccurredAt,
                DateTimeOffset.UtcNow);

            try
            {
                string? resultRef = null;

                switch (op.OperationType.ToLowerInvariant())
                {
                    case "recordmilking":
                        {
                            var cmd = JsonSerializer.Deserialize<RecordMilkingSessionCommand>(rawJson, JsonOptions);
                            if (cmd is null) throw new InvalidOperationException("Payload de ordeño inválido.");
                            var res = await sender.Send(cmd, cancellationToken);
                            resultRef = res.ToString();
                            break;
                        }

                    case "recordanimalevent":
                        {
                            var cmd = JsonSerializer.Deserialize<RecordAnimalEventCommand>(rawJson, JsonOptions);
                            if (cmd is null) throw new InvalidOperationException("Payload de evento inválido.");
                            var res = await sender.Send(cmd, cancellationToken);
                            resultRef = res.ToString();
                            break;
                        }

                    case "createanimal":
                        {
                            var cmd = JsonSerializer.Deserialize<RegisterAnimalCommand>(rawJson, JsonOptions);
                            if (cmd is null) throw new InvalidOperationException("Payload de animal inválido.");
                            var res = await sender.Send(cmd, cancellationToken);
                            resultRef = res.ToString();
                            break;
                        }

                    default:
                        throw new InvalidOperationException($"Tipo de operación no soportado: '{op.OperationType}'.");
                }

                syncOp.MarkAccepted(resultRef);
                peopleDb.SyncOperations.Add(syncOp);
                await peopleDb.SaveChangesAsync(cancellationToken);

                results.Add(new SyncOperationResultDto(
                    op.ClientOperationId,
                    "Accepted",
                    resultRef,
                    null));
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException?.Message ?? ex.Message;
                syncOp.MarkRejected(errorMsg);

                peopleDb.SyncOperations.Add(syncOp);
                await peopleDb.SaveChangesAsync(cancellationToken);

                results.Add(new SyncOperationResultDto(
                    op.ClientOperationId,
                    "Rejected",
                    null,
                    errorMsg));
            }
        }

        return new PushSyncBatchResponseDto(results.Count, results);
    }
}
