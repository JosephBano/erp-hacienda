using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hato.Modules.People.Domain;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// Feature 0008 Commit 3 integration tests (T3.1, T3.2, T3.4, T3.5):
/// Verifies the scoping of /sync/operations to the caller's own user unless they hold
/// people.users.manage, in which case they can supervise globally across all users (D3).
/// Also verifies that requesting a forbidden collection in /sync/pull returns it empty (T3.4).
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncOperationsPermissionTests(SyncApiFactory factory)
{
    /// <summary>
    /// Anonymous request to /api/v1/sync/operations returns 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task GetOperations_AnonymousUser_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/sync/operations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// T3.1 / T3.5: Regular employee without people.users.manage sees only their own sync operations.
    /// </summary>
    [Fact]
    public async Task GetOperations_RegularEmployee_SeesOnlyTheirOwnOperations()
    {
        // 1. Arrange: provision a species via admin for animal creation
        var admin = await SyncTestContext.AdminClientAsync(factory);
        var speciesResponse = await admin.PostAsJsonAsync("/api/v1/species", new { name = $"Species-{Guid.NewGuid():N}" });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesJson = await speciesResponse.Content.ReadFromJsonAsync<JsonElement>();
        var speciesId = speciesJson.GetProperty("id").GetGuid();

        // Provision two distinct employees without people.users.manage
        var employeeA = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "sync-emp-a", SystemPermissions.LivestockAnimalsWrite);
        var employeeB = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "sync-emp-b", SystemPermissions.LivestockAnimalsWrite);

        var opAId = Guid.NewGuid();
        var animalAId = Guid.NewGuid();
        await employeeA.PushAsync("createAnimal", new { id = animalAId, speciesId, sex = "Female" }, opAId);

        var opBId = Guid.NewGuid();
        var animalBId = Guid.NewGuid();
        await employeeB.PushAsync("createAnimal", new { id = animalBId, speciesId, sex = "Female" }, opBId);

        // 2. Act: query operations as employee A and employee B
        var opsA = await employeeA.GetSyncOperationsAsync();
        var opsB = await employeeB.GetSyncOperationsAsync();

        // 3. Assert: Employee A sees their own operation but never Employee B's
        Assert.Contains(opsA, o => o.GetProperty("clientOperationId").GetString() == opAId.ToString());
        Assert.DoesNotContain(opsA, o => o.GetProperty("clientOperationId").GetString() == opBId.ToString());

        // Employee B sees their own operation but never Employee A's
        Assert.Contains(opsB, o => o.GetProperty("clientOperationId").GetString() == opBId.ToString());
        Assert.DoesNotContain(opsB, o => o.GetProperty("clientOperationId").GetString() == opAId.ToString());
    }

    /// <summary>
    /// T3.2 / T3.5: Supervisor with people.users.manage sees global sync operations across all users.
    /// </summary>
    [Fact]
    public async Task GetOperations_SupervisorWithPeopleUsersManage_SeesGlobalOperations()
    {
        // 1. Arrange: provision species and two operations from distinct employees
        var admin = await SyncTestContext.AdminClientAsync(factory);
        var speciesResponse = await admin.PostAsJsonAsync("/api/v1/species", new { name = $"Species-{Guid.NewGuid():N}" });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesJson = await speciesResponse.Content.ReadFromJsonAsync<JsonElement>();
        var speciesId = speciesJson.GetProperty("id").GetGuid();

        var employeeA = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "sync-sup-emp-a", SystemPermissions.LivestockAnimalsWrite);
        var employeeB = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "sync-sup-emp-b", SystemPermissions.LivestockAnimalsWrite);

        var opAId = Guid.NewGuid();
        var animalAId = Guid.NewGuid();
        await employeeA.PushAsync("createAnimal", new { id = animalAId, speciesId, sex = "Female" }, opAId);

        var opBId = Guid.NewGuid();
        var animalBId = Guid.NewGuid();
        await employeeB.PushAsync("createAnimal", new { id = animalBId, speciesId, sex = "Female" }, opBId);

        // Provision a supervisor with people.users.manage (and no livestock permissions)
        var supervisor = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "sync-supervisor", SystemPermissions.PeopleUsersManage);

        // 2. Act: supervisor queries operations
        var supervisorOps = await supervisor.GetSyncOperationsAsync();

        // 3. Assert: Supervisor sees operations from both Employee A and Employee B
        Assert.Contains(supervisorOps, o => o.GetProperty("clientOperationId").GetString() == opAId.ToString());
        Assert.Contains(supervisorOps, o => o.GetProperty("clientOperationId").GetString() == opBId.ToString());

        // Admin (which also carries people.users.manage) also sees both operations
        var adminContext = await SyncTestContext.CreateAsync(factory, "sync-admin-supervision");
        var adminOps = await adminContext.GetSyncOperationsAsync();
        Assert.Contains(adminOps, o => o.GetProperty("clientOperationId").GetString() == opAId.ToString());
        Assert.Contains(adminOps, o => o.GetProperty("clientOperationId").GetString() == opBId.ToString());
    }

    /// <summary>
    /// T3.4 / Criterio 3: Requesting a forbidden collection explicitly by name in /sync/pull
    /// returns an empty collection and does not leak unauthorized data.
    /// </summary>
    [Fact]
    public async Task Pull_RequestingForbiddenCollectionExplicitlyByName_ReturnsEmptyCollection()
    {
        // 1. Arrange: seed an inventory item via admin
        var admin = await SyncTestContext.AdminClientAsync(factory);
        var itemResponse = await admin.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = $"Item-{Guid.NewGuid():N}",
            category = "Medicine",
            unit = "ml",
            minStock = 0,
        });
        itemResponse.EnsureSuccessStatusCode();

        // User with only livestock permission (no inventory permission)
        var limitedUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "sync-pull-forbidden", SystemPermissions.LivestockAnimalsRead);

        // 2. Act: pull specifically requesting the forbidden collection 'inventoryItems'
        var pull = await limitedUser.PullAsync(collections: "inventoryItems", batchSize: 1000);

        // 3. Assert: inventoryItems is returned empty
        var collections = pull.GetProperty("collections");
        var items = collections.GetProperty("inventoryItems");
        Assert.Empty(items.EnumerateArray());
    }
}
