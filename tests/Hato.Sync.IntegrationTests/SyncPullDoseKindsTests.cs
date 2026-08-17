using System.Net.Http.Json;
using System.Text.Json;
using Hato.Modules.People.Domain;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// docs/spec/plan-0002-fase-3-5/sub-planes/3.5a.2-C.md sec.C.1: `VaccinateScreen` and `TreatScreen`
/// need to resolve a `DoseKindId` offline (Art. 9) to build the
/// `createTreatmentCourse` payload — the same mirror pattern
/// `administrationRoutes`/`treatmentReasons` already use
/// (<see cref="SyncPullTreatmentCatalogsTests"/>), gated by the same
/// permission.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncPullDoseKindsTests(SyncApiFactory factory)
{
    [Fact]
    public async Task Pull_DeliversTheSeededDoseKindsToAnyoneWithLivestockRead()
    {
        var admin = await SyncTestContext.AdminClientAsync(factory);

        var pull = await PullAsync(admin, "doseKinds");
        var kinds = pull.GetProperty("collections").GetProperty("doseKinds").EnumerateArray().ToList();

        // Seeded by 20260809051435_SeedDoseKinds: absolute, per_weight, per_head.
        var keys = kinds.Select(k => k.GetProperty("key").GetString()).ToHashSet();
        Assert.Contains("absolute", keys);
        Assert.Contains("per_weight", keys);
        Assert.Contains("per_head", keys);
        Assert.All(kinds, k => Assert.True(k.GetProperty("isActive").GetBoolean()));
    }

    [Fact]
    public async Task Pull_HidesDoseKindsFromUsersWithoutLivestockRead()
    {
        var limited = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "no-livestock-dosekinds", SystemPermissions.InventoryItemsRead);

        var pull = await PullAsync(limited.Client, "doseKinds");

        Assert.Empty(pull.GetProperty("collections").GetProperty("doseKinds").EnumerateArray());
    }

    private static async Task<JsonElement> PullAsync(HttpClient client, string collections)
    {
        var url = $"/api/v1/sync/pull?collections={Uri.EscapeDataString(collections)}&batchSize=1000";
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
