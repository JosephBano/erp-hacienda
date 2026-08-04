using System.Net.Http.Json;
using System.Text.Json;
using Hato.Modules.People.Domain;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// PLAN-FASE-3-4 §3.A, `sync-protocol-pull` task 4: "el empleado solo baja lo que le
/// corresponde". Before this, the pull ignored the caller's permissions entirely — any
/// authenticated employee downloaded the whole farm, including modules their role has no
/// business touching.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncPullPermissionTests(SyncApiFactory factory)
{
    [Fact]
    public async Task User_WithNoLivestockPermission_ReceivesNoAnimalData()
    {
        var admin = await SyncTestContext.AdminClientAsync(factory);
        var speciesId = await CreateSpeciesAsAsync(admin);
        await CreateAnimalAsAsync(admin, speciesId);

        var limited = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "no-livestock", SystemPermissions.InventoryItemsRead);

        var pull = await limited.PullAsync(batchSize: 1000);

        Assert.Empty(pull.GetProperty("collections").GetProperty("animals").EnumerateArray());
        Assert.Empty(pull.GetProperty("collections").GetProperty("species").EnumerateArray());
    }

    [Fact]
    public async Task User_WithLivestockReadPermission_ReceivesAnimalData()
    {
        var admin = await SyncTestContext.AdminClientAsync(factory);
        var speciesId = await CreateSpeciesAsAsync(admin);
        var animalId = await CreateAnimalAsAsync(admin, speciesId);

        var limited = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "with-livestock", SystemPermissions.LivestockAnimalsRead);

        var animals = await limited.CollectAsync("animals");

        Assert.Contains(animals, row => row.GetProperty("id").GetGuid() == animalId);
    }

    [Fact]
    public async Task User_WithoutInventoryPermission_DoesNotReceiveInventoryItems()
    {
        var admin = await SyncTestContext.AdminClientAsync(factory);
        await CreateInventoryItemAsAsync(admin);

        var limited = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "no-inventory", SystemPermissions.LivestockAnimalsRead);

        var pull = await limited.PullAsync(batchSize: 1000);

        Assert.Empty(pull.GetProperty("collections").GetProperty("inventoryItems").EnumerateArray());
    }

    [Fact]
    public async Task User_WithInventoryPermission_ReceivesInventoryItems()
    {
        var admin = await SyncTestContext.AdminClientAsync(factory);
        var itemId = await CreateInventoryItemAsAsync(admin);

        var limited = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "yes-inventory", SystemPermissions.InventoryItemsRead);

        var items = await limited.CollectAsync("inventoryItems");

        Assert.Contains(items, row => row.GetProperty("id").GetGuid() == itemId);
    }

    /// <summary>Admin's role already carries every permission from the RBAC seed — no special-case bypass needed.</summary>
    [Fact]
    public async Task AdminUser_ReceivesEveryCollection()
    {
        var admin = await SyncTestContext.AdminClientAsync(factory);
        var speciesId = await CreateSpeciesAsAsync(admin);
        var animalId = await CreateAnimalAsAsync(admin, speciesId);
        var itemId = await CreateInventoryItemAsAsync(admin);

        var adminContext = await SyncTestContext.CreateAsync(factory, "admin-sees-all");

        var animals = await adminContext.CollectAsync("animals");
        var items = await adminContext.CollectAsync("inventoryItems");

        Assert.Contains(animals, row => row.GetProperty("id").GetGuid() == animalId);
        Assert.Contains(items, row => row.GetProperty("id").GetGuid() == itemId);
    }

    /// <summary>An explicit `collections=` filter still cannot leak data the role has no permission for.</summary>
    [Fact]
    public async Task RequestingAForbiddenCollectionByName_StillReturnsItEmpty()
    {
        var admin = await SyncTestContext.AdminClientAsync(factory);
        await CreateInventoryItemAsAsync(admin);

        var limited = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "explicit-forbidden", SystemPermissions.LivestockAnimalsRead);

        var pull = await limited.PullAsync(collections: "inventoryItems", batchSize: 1000);

        Assert.Empty(pull.GetProperty("collections").GetProperty("inventoryItems").EnumerateArray());
    }

    private static async Task<Guid> CreateSpeciesAsAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/species", new { name = $"Especie-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateAnimalAsAsync(HttpClient client, Guid speciesId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Female" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateInventoryItemAsAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = $"Item-{Guid.NewGuid():N}",
            category = "Medicine",
            unit = "ml",
            minStock = 0,
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}
