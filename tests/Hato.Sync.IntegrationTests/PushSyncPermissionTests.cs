using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.People.Domain;
using Hato.Modules.Production.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// Feature 0008 Commit 1 integration tests:
/// Verifies the permission map and enforcement in PushSyncBatchCommandHandler.
/// </summary>
[Collection(SyncCollection.Name)]
public class PushSyncPermissionTests(SyncApiFactory factory)
{
    /// <summary>
    /// T1.5: Denied operation leaves domain data unchanged.
    /// Attempting to create an animal without livestock.animals.write leaves no animal in db.
    /// </summary>
    [Fact]
    public async Task Push_CreateAnimal_WithoutLivestockAnimalsWrite_IsRejectedAndLeavesDomainDataUnchanged()
    {
        // 1. Arrange: seed a species via admin
        var admin = await SyncTestContext.AdminClientAsync(factory);
        var speciesResponse = await admin.PostAsJsonAsync("/api/v1/species", new { name = $"Especie-{Guid.NewGuid():N}" });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesBody = await speciesResponse.Content.ReadFromJsonAsync<JsonElement>();
        var speciesId = speciesBody.GetProperty("id").GetGuid();

        // Limited user with only Milking permission (no livestock.animals.write)
        var limitedUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "sync-deny-create-animal", SystemPermissions.ProductionMilkingRecord);

        var clientAnimalId = Guid.NewGuid();
        var operationId = Guid.NewGuid();

        // 2. Act: push createAnimal operation
        var result = await limitedUser.PushAsync(
            "createAnimal",
            new { id = clientAnimalId, speciesId, sex = "Female" },
            clientOperationId: operationId);

        // 3. Assert response: Rejected with clear reason
        Assert.Equal("Rejected", result.GetProperty("status").GetString());
        Assert.Null(result.GetProperty("resultRef").GetString());
        var errorDetails = result.GetProperty("errorDetails").GetString();
        Assert.NotNull(errorDetails);
        Assert.Contains(SystemPermissions.LivestockAnimalsWrite, errorDetails);

        // 4. Assert domain invariant: animal was NOT created in PostgreSQL database
        using var scope = factory.Services.CreateScope();
        var livestockDb = scope.ServiceProvider.GetRequiredService<ILivestockDbContext>();
        var animalInDb = await livestockDb.Animals.AsNoTracking().FirstOrDefaultAsync(a => a.Id == clientAnimalId);
        Assert.Null(animalInDb);

        // Verify via endpoint as well
        var getAnimalResponse = await admin.GetAsync($"/api/v1/animals/{clientAnimalId}");
        Assert.Equal(HttpStatusCode.NotFound, getAnimalResponse.StatusCode);

        // 5. Assert audit/sync log: operation was recorded as Rejected in server-side operation log
        var rejectedOps = await limitedUser.GetSyncOperationsAsync("Rejected");
        Assert.Contains(rejectedOps, o => o.GetProperty("clientOperationId").GetString() == operationId.ToString());
    }

    /// <summary>
    /// T1.6: Mixed push: authorized operation (e.g. milking if user has milking permission) succeeds,
    /// unauthorized operation (e.g. createAnimal) fails with individual reason, and valid one is processed.
    /// </summary>
    [Fact]
    public async Task Push_MixedBatch_ProcessesAuthorizedOperation_RejectsUnauthorizedWithoutSideEffects()
    {
        // 1. Arrange: admin seeds species and an existing cow for milking
        var admin = await SyncTestContext.AdminClientAsync(factory);
        var speciesResponse = await admin.PostAsJsonAsync("/api/v1/species", new { name = $"Especie-{Guid.NewGuid():N}", isMilkable = true });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesBody = await speciesResponse.Content.ReadFromJsonAsync<JsonElement>();
        var speciesId = speciesBody.GetProperty("id").GetGuid();

        var cowResponse = await admin.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Female" });
        cowResponse.EnsureSuccessStatusCode();
        var cowBody = await cowResponse.Content.ReadFromJsonAsync<JsonElement>();
        var cowId = cowBody.GetProperty("id").GetGuid();

        // Limited user with only production.milking.record
        var limitedUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "sync-mixed-batch", SystemPermissions.ProductionMilkingRecord);

        var milkingOpId = Guid.NewGuid();
        var animalOpId = Guid.NewGuid();
        var unauthorizedAnimalId = Guid.NewGuid();

        var batchPayload = new object[]
        {
            new
            {
                clientOperationId = milkingOpId,
                operationType = "recordMilking",
                occurredAt = DateTimeOffset.UtcNow,
                payload = (object)new
                {
                    date = DateOnly.FromDateTime(DateTime.UtcNow),
                    shift = "Morning",
                    recordedBy = "operador-ordeno",
                    totalLiters = 16.5m,
                    individualYields = new[]
                    {
                        new { animalId = cowId, liters = 16.5m }
                    },
                    isPlausibilityConfirmed = true,
                },
            },
            new
            {
                clientOperationId = animalOpId,
                operationType = "createAnimal",
                occurredAt = DateTimeOffset.UtcNow,
                payload = (object)new
                {
                    id = unauthorizedAnimalId,
                    speciesId,
                    sex = "Female",
                },
            },
        };

        // 2. Act: Push mixed batch
        var batchResponse = await limitedUser.PushBatchAsync("device-mixed", batchPayload);

        // 3. Assert results: 2 operations evaluated individually
        var results = batchResponse.GetProperty("results").EnumerateArray().ToList();
        Assert.Equal(2, results.Count);

        // Op 1 (Milking) -> Accepted
        var milkingResult = results.First(r => r.GetProperty("clientOperationId").GetString() == milkingOpId.ToString());
        Assert.Equal("Accepted", milkingResult.GetProperty("status").GetString());
        var milkingRef = milkingResult.GetProperty("resultRef").GetString();
        Assert.NotNull(milkingRef);
        var sessionId = Guid.Parse(milkingRef);

        // Op 2 (createAnimal) -> Rejected
        var animalResult = results.First(r => r.GetProperty("clientOperationId").GetString() == animalOpId.ToString());
        Assert.Equal("Rejected", animalResult.GetProperty("status").GetString());
        Assert.Null(animalResult.GetProperty("resultRef").GetString());
        var errorDetails = animalResult.GetProperty("errorDetails").GetString();
        Assert.NotNull(errorDetails);
        Assert.Contains(SystemPermissions.LivestockAnimalsWrite, errorDetails);

        // 4. Assert domain state in DB:
        using var scope = factory.Services.CreateScope();

        // Milking session was created in ProductionDbContext
        var productionDb = scope.ServiceProvider.GetRequiredService<IProductionDbContext>();
        var milkingSession = await productionDb.MilkingSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId);
        Assert.NotNull(milkingSession);
        Assert.Equal(16.5m, milkingSession.TotalLiters);

        // Animal was NOT created in LivestockDbContext
        var livestockDb = scope.ServiceProvider.GetRequiredService<ILivestockDbContext>();
        var rejectedAnimal = await livestockDb.Animals.AsNoTracking().FirstOrDefaultAsync(a => a.Id == unauthorizedAnimalId);
        Assert.Null(rejectedAnimal);
    }

    /// <summary>
    /// T1.4: Operation with unknown/invented operationType is rejected.
    /// Does not pass by omission; rejected before calling ExecuteAsync.
    /// </summary>
    [Fact]
    public async Task Push_UnknownOperationType_IsRejectedWithClearReason()
    {
        var context = await SyncTestContext.CreateAsync(factory, "sync-unknown-op");
        var operationId = Guid.NewGuid();

        var result = await context.PushAsync(
            "destroyAllPasturesInvented",
            new { foo = "bar" },
            clientOperationId: operationId);

        Assert.Equal("Rejected", result.GetProperty("status").GetString());
        Assert.Null(result.GetProperty("resultRef").GetString());

        var errorDetails = result.GetProperty("errorDetails").GetString();
        Assert.NotNull(errorDetails);
        Assert.Contains("Tipo de operación no soportado: 'destroyAllPasturesInvented'.", errorDetails);

        // Verify recorded in sync log
        var rejectedOps = await context.GetSyncOperationsAsync("Rejected");
        Assert.Contains(rejectedOps, o => o.GetProperty("clientOperationId").GetString() == operationId.ToString());
    }

    /// <summary>
    /// T1.7: Anonymous request to /api/v1/sync/push returns 401.
    /// </summary>
    [Fact]
    public async Task Push_AnonymousRequest_Returns401Unauthorized()
    {
        var anonymousClient = factory.CreateClient();

        var response = await anonymousClient.PostAsJsonAsync("/api/v1/sync/push", new
        {
            deviceId = "device-unauthenticated",
            operations = new object[]
            {
                new
                {
                    clientOperationId = Guid.NewGuid(),
                    operationType = "createAnimal",
                    occurredAt = DateTimeOffset.UtcNow,
                    payload = new { sex = "Female" },
                },
            },
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// T1.3: Multi-command operation moveAnimal explicitly requires livestock.animals.write.
    /// </summary>
    [Fact]
    public async Task Push_MoveAnimal_WithoutLivestockAnimalsWrite_IsRejected()
    {
        var limitedUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "sync-deny-move", SystemPermissions.ProductionMilkingRecord);

        var operationId = Guid.NewGuid();
        var animalId = Guid.NewGuid();
        var targetGroupId = Guid.NewGuid();

        var result = await limitedUser.PushAsync(
            "moveAnimal",
            new { animalId, toGroupId = targetGroupId },
            clientOperationId: operationId);

        Assert.Equal("Rejected", result.GetProperty("status").GetString());
        var errorDetails = result.GetProperty("errorDetails").GetString();
        Assert.NotNull(errorDetails);
        Assert.Contains(SystemPermissions.LivestockAnimalsWrite, errorDetails);
    }

    /// <summary>
    /// Admin user has full permission and can push any valid operation.
    /// </summary>
    [Fact]
    public async Task Push_AdminUser_SucceedsForAuthorizedOperations()
    {
        var adminContext = await SyncTestContext.CreateAsync(factory, "sync-admin-push");
        var speciesId = await adminContext.CreateSpeciesAsync("Bovino");
        var animalId = Guid.NewGuid();

        var result = await adminContext.PushAsync(
            "createAnimal",
            new { id = animalId, speciesId, sex = "Female" });

        Assert.Equal("Accepted", result.GetProperty("status").GetString());
        Assert.Equal(animalId.ToString(), result.GetProperty("resultRef").GetString());

        using var scope = factory.Services.CreateScope();
        var livestockDb = scope.ServiceProvider.GetRequiredService<ILivestockDbContext>();
        var animal = await livestockDb.Animals.AsNoTracking().FirstOrDefaultAsync(a => a.Id == animalId);
        Assert.NotNull(animal);
    }
}
