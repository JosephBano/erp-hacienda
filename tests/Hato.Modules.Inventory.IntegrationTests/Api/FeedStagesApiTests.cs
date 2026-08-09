using System.Net;
using System.Net.Http.Json;
using Hato.Modules.Inventory.Application.FeedStages;
using Hato.Modules.Inventory.Application.Items;
using Xunit;

namespace Hato.Modules.Inventory.IntegrationTests.Api;

/// <summary>
/// PLAN-FASE-3-5-PORCINO.md sec.3.5a.5 task 3: feed_stage catalog. Verifies the seed
/// lands via the real migration, the listing endpoint reads it back through Postgres,
/// and the domain invariant ("only Feed items may declare a stage") holds end-to-end
/// through the HTTP surface, not just in the unit tests.
/// </summary>
public class FeedStagesApiTests(InventoryApiFactory factory) : IClassFixture<InventoryApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetFeedStages_ReturnsTheSeededCatalog()
    {
        var response = await _client.GetAsync("/api/v1/inventory/feed-stages");
        response.EnsureSuccessStatusCode();

        var stages = await response.Content.ReadFromJsonAsync<List<FeedStageDto>>();

        Assert.NotNull(stages);
        Assert.Equal(6, stages!.Count);
        Assert.All(stages, s => Assert.True(s.IsActive));

        var keys = stages.Select(s => s.Key).ToHashSet();
        Assert.Equal(
            new HashSet<string> { "pre_starter", "starter", "grower", "finisher", "gestation", "lactation" },
            keys);
    }

    [Fact]
    public async Task CreateFeedItem_WithFeedStage_Succeeds()
    {
        var stages = await _client.GetFromJsonAsync<List<FeedStageDto>>("/api/v1/inventory/feed-stages");
        var starter = stages!.Single(s => s.Key == "starter");

        var response = await _client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = $"Balanceado-{Guid.NewGuid():N}",
            category = "Feed",
            unit = "kg",
            feedStageId = starter.Id,
        });

        response.EnsureSuccessStatusCode();
        var itemId = (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var items = await _client.GetFromJsonAsync<List<InventoryItemDto>>("/api/v1/inventory/items?category=Feed");
        var item = items!.Single(i => i.Id == itemId);

        Assert.Equal(starter.Id, item.FeedStageId);
    }

    [Fact]
    public async Task CreateNonFeedItem_WithFeedStage_IsRejected()
    {
        var stages = await _client.GetFromJsonAsync<List<FeedStageDto>>("/api/v1/inventory/feed-stages");
        var grower = stages!.Single(s => s.Key == "grower");

        var response = await _client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = $"Oxitetraciclina-{Guid.NewGuid():N}",
            category = "Medicine",
            unit = "ml",
            feedStageId = grower.Id,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateFeedItem_WithUnknownFeedStageId_IsRejected()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = $"Balanceado-{Guid.NewGuid():N}",
            category = "Feed",
            unit = "kg",
            feedStageId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record CreatedId(Guid Id);
}
