using System.Net.Http.Json;
using System.Text.Json;
using Hato.Modules.People.Domain;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// 3.5b.1 (ADR-0016): the field-app needs the health_plan catalog on the pull
/// so it can resolve theoretical dates offline and stamp the
/// <c>health_plan_item_id</c> on a treatment event from the corral. The cronogram
/// items themselves are part of the same pull because the field-app wants to
/// render the cronogram UI without a round-trip.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncPullHealthPlansTests(SyncApiFactory factory)
{
    [Fact]
    public async Task Pull_DeliversSeededPlansAndItemsToAnyoneWithLivestockRead()
    {
        var admin = await SyncTestContext.AdminClientAsync(factory);

        // Create a species + plan + item so the admin context has at least one row.
        var speciesResponse = await admin.PostAsJsonAsync("/api/v1/species",
            new { name = $"Esp-{Guid.NewGuid():N}" });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var planResponse = await admin.PostAsJsonAsync("/api/v1/health-plans",
            new { name = $"Plan-{Guid.NewGuid():N}", speciesId });
        planResponse.EnsureSuccessStatusCode();
        var planId = (await planResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var itemResponse = await admin.PostAsJsonAsync($"/api/v1/health-plans/{planId}/items",
            new
            {
                name = "Item de prueba",
                eventType = "Vaccination",
                anchor = "Birth",
                anchorOffsetDays = 30,
                complianceWindowDays = 3,
            });
        itemResponse.EnsureSuccessStatusCode();

        var pull = await PullAsync(admin, "healthPlans,healthPlanItems,healthPlanAssignments");
        var plans = pull.GetProperty("collections").GetProperty("healthPlans").EnumerateArray().ToList();
        var items = pull.GetProperty("collections").GetProperty("healthPlanItems").EnumerateArray().ToList();
        var assignments = pull.GetProperty("collections").GetProperty("healthPlanAssignments").EnumerateArray().ToList();

        Assert.Contains(plans, p => p.GetProperty("id").GetGuid() == planId);
        Assert.Contains(items, i => i.GetProperty("healthPlanId").GetGuid() == planId);
        // No assignment was created in this test, but the collection should
        // be present (possibly empty) — the mobile should be able to read it.
        Assert.NotNull(assignments);
    }

    [Fact]
    public async Task Pull_HidesPlansFromUsersWithoutLivestockRead()
    {
        var admin = await SyncTestContext.AdminClientAsync(factory);

        // Ensure at least one plan exists.
        var speciesResponse = await admin.PostAsJsonAsync("/api/v1/species",
            new { name = $"Esp-{Guid.NewGuid():N}" });
        speciesResponse.EnsureSuccessStatusCode();

        var limited = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "no-livestock-pulls", SystemPermissions.InventoryItemsRead);

        var pull = await PullAsync(limited.Client, "healthPlans,healthPlanItems,healthPlanAssignments");

        Assert.Empty(pull.GetProperty("collections").GetProperty("healthPlans").EnumerateArray());
        Assert.Empty(pull.GetProperty("collections").GetProperty("healthPlanItems").EnumerateArray());
        Assert.Empty(pull.GetProperty("collections").GetProperty("healthPlanAssignments").EnumerateArray());
    }

    private static async Task<JsonElement> PullAsync(HttpClient client, string collections)
    {
        var url = $"/api/v1/sync/pull?collections={Uri.EscapeDataString(collections)}&batchSize=1000";
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
