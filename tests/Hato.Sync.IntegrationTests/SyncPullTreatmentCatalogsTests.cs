using System.Net.Http.Json;
using System.Text.Json;
using Hato.Modules.People.Domain;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// 3.5a.2-A — the field app needs the new administration_routes and
/// treatment_reasons catalogues on the pull, gated by livestock.animals.read
/// (the same permission that gates species/breeds/categories). Without them
/// the field app cannot build a structured treatment payload offline — the
/// whole point of 3.5a.2.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncPullTreatmentCatalogsTests(SyncApiFactory factory)
{
    [Fact]
    public async Task Pull_DeliversTheSeededRoutesAndReasonsToAnyoneWithLivestockRead()
    {
        var admin = await SyncTestContext.AdminClientAsync(factory);

        // Confirm the seeds landed in the test database
        // (docs/planes/fase-3-5/sub-planes/3.5a.2-A.md sec."Tareas" puntos 1-2):
        // seven routes, three reasons.
        var pull = await PullAsync(admin, "administrationRoutes,treatmentReasons");
        var routes = pull.GetProperty("collections").GetProperty("administrationRoutes").EnumerateArray().ToList();
        var reasons = pull.GetProperty("collections").GetProperty("treatmentReasons").EnumerateArray().ToList();

        Assert.Equal(7, routes.Count);
        Assert.Equal(3, reasons.Count);

        var routeKeys = routes.Select(r => r.GetProperty("key").GetString()).ToHashSet();
        Assert.Contains("oral_water", routeKeys);
        Assert.Contains("im", routeKeys);
        Assert.Contains("intrauterina", routeKeys);

        var reasonKeys = reasons.Select(r => r.GetProperty("key").GetString()).ToHashSet();
        Assert.Contains("scheduled", reasonKeys);
        Assert.Contains("curative", reasonKeys);
        Assert.Contains("preventive", reasonKeys);

        // A new route inserted via the panel API also arrives on the next
        // pull, so a field deployed before the change still catches up
        // (Art. 9: offline-first without silent data loss).
        var newKey = $"pull_test_{Guid.NewGuid():N}";
        var create = await admin.PostAsJsonAsync("/api/v1/administration-routes",
            new { key = newKey, labelEs = "Vía de prueba pull" });
        create.EnsureSuccessStatusCode();

        var secondPull = await PullAsync(admin, "administrationRoutes");
        var secondRoutes = secondPull.GetProperty("collections").GetProperty("administrationRoutes").EnumerateArray();
        Assert.Contains(secondRoutes, r => r.GetProperty("key").GetString() == newKey);
    }

    [Fact]
    public async Task Pull_HidesRoutesAndReasonsFromUsersWithoutLivestockRead()
    {
        var admin = await SyncTestContext.AdminClientAsync(factory);

        // Make sure at least one row exists so the absence-on-pull test
        // isn't a no-op against an empty DB.
        (await admin.PostAsJsonAsync("/api/v1/administration-routes",
            new { key = $"x_{Guid.NewGuid():N}", labelEs = "X" })).EnsureSuccessStatusCode();

        var limited = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "no-livestock-pulls", SystemPermissions.InventoryItemsRead);

        var pull = await PullAsync(limited.Client, "administrationRoutes,treatmentReasons");

        Assert.Empty(pull.GetProperty("collections").GetProperty("administrationRoutes").EnumerateArray());
        Assert.Empty(pull.GetProperty("collections").GetProperty("treatmentReasons").EnumerateArray());
    }

    [Fact]
    public async Task Pull_MarksDeactivatedRoutesWithIsActiveFalse_ButStillDeliversThem()
    {
        // Art. 1: history is never erased. The phone must be able to render a
        // route on a historical TreatmentEvent even after the panel retires it
        // from new use.
        var admin = await SyncTestContext.AdminClientAsync(factory);
        var create = await admin.PostAsJsonAsync("/api/v1/administration-routes",
            new { key = $"retired_{Guid.NewGuid():N}", labelEs = "Retirada" });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await admin.DeleteAsync($"/api/v1/administration-routes/{id}")).EnsureSuccessStatusCode();

        // The pull always returns ALL rows (active + inactive) — the
        // mobile filters at the consumer side via IsActive. That mirrors
        // the existing mortalityCauses behaviour and keeps the wire
        // format simple (one cursor for the whole collection).
        var pull = await PullAsync(admin, "administrationRoutes");
        var rows = pull.GetProperty("collections").GetProperty("administrationRoutes").EnumerateArray().ToList();
        var row = Assert.Single(rows, r => r.GetProperty("id").GetGuid() == id);
        Assert.False(row.GetProperty("isActive").GetBoolean());
    }

    private static async Task<JsonElement> PullAsync(HttpClient client, string collections)
    {
        var url = $"/api/v1/sync/pull?collections={Uri.EscapeDataString(collections)}&batchSize=1000";
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}