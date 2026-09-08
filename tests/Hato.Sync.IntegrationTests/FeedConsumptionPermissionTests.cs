using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Hato.Modules.Inventory.Application.Abstractions;
using Hato.Modules.People.Application.Auth;
using Hato.Modules.People.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// Feature 0008 Commit 5 integration tests (T5.1 - T5.7):
/// Verifies the feed consumption permission enforcement:
/// - Anonymous gets 401 on REST and Push.
/// - User with inventory.feed-consumptions.record can record consumption via REST (201) and Push (Accepted).
/// - User with only inventory.feed-consumptions.record gets 403 when attempting to manage items catalogue
///   (POST /api/v1/inventory/items, POST /api/v1/inventory/feed-stages, POST /batches).
/// - User without inventory.feed-consumptions.record gets 403 on REST and Rejected on push with domain unchanged.
/// - Registrar role seeded with inventory.feed-consumptions.record can record consumption without catalogue management.
/// Tested against real PostgreSQL.
/// </summary>
[Collection(SyncCollection.Name)]
public class FeedConsumptionPermissionTests(SyncApiFactory factory)
{
    private async Task<(Guid groupId, Guid itemId)> SeedFeedStockAsync(SyncTestContext adminContext, decimal initialStock = 500m)
    {
        var groupId = await adminContext.CreateGroupAsync("Lote-Cerdos-Alimentacion", trackingMode: "Headcount");
        var itemId = await adminContext.CreateInventoryItemAsync(name: $"Alimento-{Guid.NewGuid():N}", category: "Feed", unit: "kg");
        await adminContext.CreateInventoryBatchAsync(itemId, $"BATCH-{Guid.NewGuid():N}"[..10], initialStock);
        return (groupId, itemId);
    }

    [Fact]
    public async Task AnonymousFeedConsumption_Returns401_OnRestAndPush()
    {
        var anonClient = factory.CreateClient();
        var randomGroupId = Guid.NewGuid();
        var randomItemId = Guid.NewGuid();

        // 1. REST route anonymous -> 401
        var restResponse = await anonClient.PostAsJsonAsync("/api/v1/inventory/feed-consumptions", new
        {
            groupId = randomGroupId,
            inventoryItemId = randomItemId,
            quantity = 25m,
            consumedAt = "2026-09-08",
            recordedBy = "anon",
            unit = "kg",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, restResponse.StatusCode);

        // 2. Push route anonymous -> 401
        var pushResponse = await anonClient.PostAsJsonAsync("/api/v1/sync/push", new
        {
            deviceId = "device-unauth",
            operations = new[]
            {
                new
                {
                    clientOperationId = Guid.NewGuid(),
                    operationType = "recordFeedConsumption",
                    occurredAt = DateTimeOffset.UtcNow,
                    payload = new
                    {
                        groupId = randomGroupId,
                        inventoryItemId = randomItemId,
                        quantity = 25m,
                        consumedAt = "2026-09-08",
                        recordedBy = "anon",
                        unit = "kg",
                    }
                }
            }
        });
        Assert.Equal(HttpStatusCode.Unauthorized, pushResponse.StatusCode);
    }

    [Fact]
    public async Task UserWithFeedConsumptionPermission_CanRecordConsumption_ViaRestAndPush()
    {
        var admin = await SyncTestContext.CreateAsync(factory, "admin-feed-prep");
        var (groupId, itemId) = await SeedFeedStockAsync(admin, initialStock: 500m);

        var feedUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "user-feed-record", SystemPermissions.InventoryFeedConsumptionsRecord);

        // 1. REST: POST /api/v1/inventory/feed-consumptions -> 201 Created
        var restResponse = await feedUser.Client.PostAsJsonAsync("/api/v1/inventory/feed-consumptions", new
        {
            groupId,
            inventoryItemId = itemId,
            quantity = 30m,
            consumedAt = "2026-09-08",
            recordedBy = "alimentador-rest",
            unit = "kg",
            notes = "Alimentación matutina REST",
        });
        Assert.Equal(HttpStatusCode.Created, restResponse.StatusCode);

        var restBody = await restResponse.Content.ReadFromJsonAsync<JsonElement>();
        var restId = restBody.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, restId);

        // 2. Push: recordFeedConsumption -> Accepted
        var pushOpId = Guid.NewGuid();
        var pushResult = await feedUser.PushAsync("recordFeedConsumption", new
        {
            groupId,
            inventoryItemId = itemId,
            quantity = 40m,
            consumedAt = "2026-09-08",
            recordedBy = "alimentador-push",
            unit = "kg",
            notes = "Alimentación vespertina Push",
        }, clientOperationId: pushOpId);

        Assert.Equal("Accepted", pushResult.GetProperty("status").GetString());
        var pushResultRef = pushResult.GetProperty("resultRef").GetString();
        Assert.NotNull(pushResultRef);
        var pushId = Guid.Parse(pushResultRef!);

        // 3. Verify both landed in the database
        using var scope = factory.Services.CreateScope();
        var inventoryDb = scope.ServiceProvider.GetRequiredService<IInventoryDbContext>();

        var restRow = await inventoryDb.GroupFeedConsumptions.FirstOrDefaultAsync(c => c.Id == restId);
        Assert.NotNull(restRow);
        Assert.Equal(30m, restRow!.QuantityRecorded);
        Assert.Equal(groupId, restRow.GroupId);

        var pushRow = await inventoryDb.GroupFeedConsumptions.FirstOrDefaultAsync(c => c.Id == pushId);
        Assert.NotNull(pushRow);
        Assert.Equal(40m, pushRow!.QuantityRecorded);
        Assert.Equal(groupId, pushRow.GroupId);
    }

    /// <summary>
    /// T5.5: User who only feeds animals can record feed consumption, BUT
    /// attempting to manage items catalogue (POST /inventory/items, POST /feed-stages, POST /batches) returns 403!
    /// This is the core rationale for having a dedicated permission instead of reusing inventory.items.manage.
    /// </summary>
    [Fact]
    public async Task UserWithFeedConsumptionPermission_CannotManageCatalogue_Returns403()
    {
        var admin = await SyncTestContext.CreateAsync(factory, "admin-catalogue-check");
        var (groupId, itemId) = await SeedFeedStockAsync(admin);

        // User with ONLY inventory.feed-consumptions.record
        var feedOnlyUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "user-only-feed", SystemPermissions.InventoryFeedConsumptionsRecord);

        // 1. Cannot create inventory item catalogue entry
        var postItemResponse = await feedOnlyUser.Client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = "Intento Item No Autorizado",
            category = "Feed",
            unit = "kg",
            minStock = 10m,
        });
        Assert.Equal(HttpStatusCode.Forbidden, postItemResponse.StatusCode);

        // 2. Cannot create feed stages
        var postStageResponse = await feedOnlyUser.Client.PostAsJsonAsync("/api/v1/inventory/feed-stages", new
        {
            name = "Etapa No Autorizada",
            order = 1,
        });
        Assert.Equal(HttpStatusCode.Forbidden, postStageResponse.StatusCode);

        // 3. Cannot manage batches on existing items
        var postBatchResponse = await feedOnlyUser.Client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/batches", new
        {
            batchNumber = "BATCH-DENIED",
            quantity = 100m,
            costPerUnit = 2.5m,
        });
        Assert.Equal(HttpStatusCode.Forbidden, postBatchResponse.StatusCode);

        // 4. Cannot manage feed stage assignment on item
        var postItemFeedStageResponse = await feedOnlyUser.Client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/feed-stage", new
        {
            feedStageId = Guid.NewGuid(),
        });
        Assert.Equal(HttpStatusCode.Forbidden, postItemFeedStageResponse.StatusCode);
    }

    [Fact]
    public async Task UserWithoutFeedConsumptionPermission_IsDenied_403OnRest_And_RejectedOnPush()
    {
        var admin = await SyncTestContext.CreateAsync(factory, "admin-no-feed-prep");
        var (groupId, itemId) = await SeedFeedStockAsync(admin, initialStock: 200m);

        // User without inventory.feed-consumptions.record (only has livestock.animals.read)
        var unauthorizedUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "user-no-feed-perm", SystemPermissions.LivestockAnimalsRead);

        // 1. REST: POST /api/v1/inventory/feed-consumptions -> 403
        var restResponse = await unauthorizedUser.Client.PostAsJsonAsync("/api/v1/inventory/feed-consumptions", new
        {
            groupId,
            inventoryItemId = itemId,
            quantity = 10m,
            consumedAt = "2026-09-08",
            recordedBy = "no-perm-user",
            unit = "kg",
        });
        Assert.Equal(HttpStatusCode.Forbidden, restResponse.StatusCode);

        // 2. Push: recordFeedConsumption -> Rejected
        var pushOpId = Guid.NewGuid();
        var pushResult = await unauthorizedUser.PushAsync("recordFeedConsumption", new
        {
            groupId,
            inventoryItemId = itemId,
            quantity = 10m,
            consumedAt = "2026-09-08",
            recordedBy = "no-perm-user",
            unit = "kg",
        }, clientOperationId: pushOpId);

        Assert.Equal("Rejected", pushResult.GetProperty("status").GetString());
        Assert.Null(pushResult.GetProperty("resultRef").GetString());
        var errorDetails = pushResult.GetProperty("errorDetails").GetString();
        Assert.NotNull(errorDetails);
        Assert.Contains(SystemPermissions.InventoryFeedConsumptionsRecord, errorDetails);

        // 3. Invariant: domain data unchanged in PostgreSQL
        using var scope = factory.Services.CreateScope();
        var inventoryDb = scope.ServiceProvider.GetRequiredService<IInventoryDbContext>();
        var consumptionsCount = await inventoryDb.GroupFeedConsumptions
            .CountAsync(c => c.GroupId == groupId && c.InventoryItemId == itemId);
        Assert.Equal(0, consumptionsCount);
    }

    [Fact]
    public async Task RegistrarRole_CanRecordFeedConsumption_WithoutCatalogueManagement()
    {
        var admin = await SyncTestContext.CreateAsync(factory, "admin-reg-prep");
        var (groupId, itemId) = await SeedFeedStockAsync(admin, initialStock: 300m);

        // Create a user with system role "registrar" (seeded in migration)
        var adminClient = await SyncTestContext.AdminClientAsync(factory);
        var email = $"registrar-test-{Guid.NewGuid():N}@finca.ec";
        const string password = "SecurePassword123!";

        var createRes = await adminClient.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Registrador Alimentos",
            email,
            password,
            role = SystemRoles.Registrar,
        });
        createRes.EnsureSuccessStatusCode();

        // Login as registrar
        var anonClient = factory.CreateClient();
        var loginRes = await anonClient.PostAsJsonAsync("/api/v1/people/auth/login", new
        {
            email,
            password,
        });
        loginRes.EnsureSuccessStatusCode();
        var loginBody = await loginRes.Content.ReadFromJsonAsync<LoginResultDto>();
        Assert.NotNull(loginBody);

        var registrarClient = factory.CreateClient();
        registrarClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginBody!.Token);

        // 1. Registrar can record feed consumption via REST (201)
        var restResponse = await registrarClient.PostAsJsonAsync("/api/v1/inventory/feed-consumptions", new
        {
            groupId,
            inventoryItemId = itemId,
            quantity = 25m,
            consumedAt = "2026-09-08",
            recordedBy = "registrador",
            unit = "kg",
        });
        Assert.Equal(HttpStatusCode.Created, restResponse.StatusCode);

        // 2. Registrar cannot manage feed stages catalogue (403)
        var stageResponse = await registrarClient.PostAsJsonAsync("/api/v1/inventory/feed-stages", new
        {
            name = "Etapa Prohibida",
            order = 1,
        });
        Assert.Equal(HttpStatusCode.Forbidden, stageResponse.StatusCode);

        // 3. Registrar cannot create inventory items catalogue entry (403)
        var itemResponse = await registrarClient.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = "Item Prohibido",
            category = "Feed",
            unit = "kg",
            minStock = 5m,
        });
        Assert.Equal(HttpStatusCode.Forbidden, itemResponse.StatusCode);
    }
}
