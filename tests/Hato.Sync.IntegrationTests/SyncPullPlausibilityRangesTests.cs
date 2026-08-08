using System.Net.Http.Json;
using System.Text.Json;
using Hato.Modules.People.Domain;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// 3.5a.6 (ADR-0022): the field-app needs the plausibility_ranges catalog on
/// the pull, gated by <c>livestock.animals.read</c>. Without it the field
/// cannot validate weights and milk volumes locally (Art. 9: offline-first
/// validation). The evaluator returns Pass when the local table is empty
/// (fail-open), but the field-app must surface the seeded defaults so the
/// plausibility confirmation dialog actually appears in the field.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncPullPlausibilityRangesTests(SyncApiFactory factory)
{
    [Fact]
    public async Task Pull_DeliversTheSeededRangesToAnyoneWithLivestockRead()
    {
        var admin = await SyncTestContext.AdminClientAsync(factory);

        var pull = await PullAsync(admin, "plausibilityRanges");
        var ranges = pull.GetProperty("collections").GetProperty("plausibilityRanges").EnumerateArray().ToList();

        // The seed installs 5 ranges (porcine/bovine/caprine weight + bovine/caprine milk).
        Assert.True(ranges.Count >= 5, $"Expected at least 5 seeded ranges, got {ranges.Count}.");

        var magnitudes = ranges.Select(r => r.GetProperty("magnitude").GetString()).ToHashSet();
        Assert.Contains("weight_kg", magnitudes);
        Assert.Contains("milk_liters", magnitudes);
    }

    [Fact]
    public async Task Pull_HidesRangesFromUsersWithoutLivestockRead()
    {
        var admin = await SyncTestContext.AdminClientAsync(factory);

        // Ensure at least one row exists so the absence-on-pull test isn't silent.
        var speciesResponse = await admin.PostAsJsonAsync("/api/v1/species",
            new { name = $"Esp-{Guid.NewGuid():N}" });
        speciesResponse.EnsureSuccessStatusCode();

        var limited = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "no-livestock-pulls", SystemPermissions.InventoryItemsRead);

        var pull = await PullAsync(limited.Client, "plausibilityRanges");

        Assert.Empty(pull.GetProperty("collections").GetProperty("plausibilityRanges").EnumerateArray());
    }

    [Fact]
    public async Task Pull_MarksDeactivatedRangesWithIsActiveFalse_ButStillDeliversThem()
    {
        // Art. 1: history is never erased. If a range is deactivated, the phone
        // must still receive it so old events can be interpreted against the
        // range that was active when they were recorded (or rendered as
        // historical read-only).
        var admin = await SyncTestContext.AdminClientAsync(factory);

        var speciesResponse = await admin.PostAsJsonAsync("/api/v1/species",
            new { name = $"Esp-{Guid.NewGuid():N}" });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var create = await admin.PostAsJsonAsync("/api/v1/plausibility-ranges",
            new
            {
                speciesId,
                categoryId = (Guid?)null,
                magnitude = "weight_kg",
                plausibleMin = 1m,
                plausibleMax = 100m,
                absoluteMin = 0m,
                absoluteMax = 200m,
            });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await admin.DeleteAsync($"/api/v1/plausibility-ranges/{id}")).EnsureSuccessStatusCode();

        var pull = await PullAsync(admin, "plausibilityRanges");
        var rows = pull.GetProperty("collections").GetProperty("plausibilityRanges").EnumerateArray().ToList();
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
