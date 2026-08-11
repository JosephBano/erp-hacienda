using System.Net;
using System.Net.Http.Json;
using Hato.Modules.Inventory.Application.FeedStages;
using Hato.Modules.Inventory.Application.Items;
using Hato.Modules.Inventory.Domain;
using Xunit;

namespace Hato.Modules.Inventory.IntegrationTests.Api;

/// <summary>
/// End-to-end coverage for the read/write endpoints introduced to expose the
/// detail page on admin-web (<c>/inventory/items/:id</c>): the read-side
/// <c>GET /items/{id}</c>, <c>GET /items/{id}/batches</c> and
/// <c>GET /items/{id}/unit-conversions</c>, the write-side
/// <c>POST /items/{id}/feed-stage</c>, and the catalog CRUD
/// (<c>POST /feed-stages</c>, <c>POST /feed-stages/{id}/activate|deactivate</c>).
///
/// Lives in this assembly — and not under the unit tests project — because the
/// handlers go through EF Core against a real PostgreSQL, and that is exactly
/// the boundary Art. 12 pins to Testcontainers (or HATO_TEST_POSTGRES).
/// </summary>
public class InventoryItemDetailApiTests(InventoryApiFactory factory) : IClassFixture<InventoryApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetItemById_ExistingItem_ReturnsDetail()
    {
        var itemId = await CreateItemAsync("Detail-Item", ItemCategory.Feed, "kg");

        var response = await _client.GetAsync($"/api/v1/inventory/items/{itemId}");

        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<InventoryItemDetailDto>();

        Assert.NotNull(detail);
        Assert.Equal(itemId, detail!.Id);
        Assert.StartsWith("Detail-Item-", detail.Name);
        Assert.Equal(ItemCategory.Feed, detail.Category);
        Assert.Equal("kg", detail.Unit);
    }

    [Fact]
    public async Task GetItemById_UnknownGuid_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/inventory/items/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBatches_ReturnsOnlyBatchesForTheRequestedItem()
    {
        var itemA = await CreateItemAsync("Batches-A", ItemCategory.Feed, "kg");
        var itemB = await CreateItemAsync("Batches-B", ItemCategory.Feed, "kg");
        await CreateBatchAsync(itemA, "L-A-1", 100);
        await CreateBatchAsync(itemA, "L-A-2", 50);
        await CreateBatchAsync(itemB, "L-B-1", 999);

        var response = await _client.GetAsync($"/api/v1/inventory/items/{itemA}/batches");

        response.EnsureSuccessStatusCode();
        var batches = await response.Content.ReadFromJsonAsync<List<InventoryBatchDto>>();

        Assert.NotNull(batches);
        Assert.Equal(2, batches!.Count);
        Assert.All(batches, b => Assert.StartsWith("L-A-", b.BatchNumber));
    }

    [Fact]
    public async Task GetUnitConversions_ReturnsOnlyConversionsForTheRequestedItem()
    {
        var itemA = await CreateItemAsync("Conv-A", ItemCategory.Feed, "kg");
        var itemB = await CreateItemAsync("Conv-B", ItemCategory.Feed, "kg");
        await RegisterConversionAsync(itemA, "saco40kg", "kg", 40m);
        await RegisterConversionAsync(itemA, "saco20kg", "kg", 20m);
        await RegisterConversionAsync(itemB, "lata", "kg", 15m);

        var response = await _client.GetAsync($"/api/v1/inventory/items/{itemA}/unit-conversions");

        response.EnsureSuccessStatusCode();
        var conversions = await response.Content.ReadFromJsonAsync<List<UnitConversionDto>>();

        Assert.NotNull(conversions);
        Assert.Equal(2, conversions!.Count);
        Assert.Equal(new[] { "saco20kg", "saco40kg" }, conversions.Select(c => c.FromUnit).ToArray());
    }

    [Fact]
    public async Task SetFeedStage_ValidStage_PersistsOnItem()
    {
        var itemId = await CreateItemAsync("Stage-Set", ItemCategory.Feed, "kg");
        var starterId = await GetFeedStageIdByKeyAsync("starter");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/inventory/items/{itemId}/feed-stage",
            new { feedStageId = starterId });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var after = await _client.GetFromJsonAsync<InventoryItemDetailDto>($"/api/v1/inventory/items/{itemId}");
        Assert.Equal(starterId, after!.FeedStageId);
    }

    [Fact]
    public async Task SetFeedStage_ClearWithNull_RemovesStage()
    {
        var itemId = await CreateItemAsync("Stage-Clear", ItemCategory.Feed, "kg", feedStageId: await GetFeedStageIdByKeyAsync("starter"));

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/inventory/items/{itemId}/feed-stage",
            new { feedStageId = (Guid?)null });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var after = await _client.GetFromJsonAsync<InventoryItemDetailDto>($"/api/v1/inventory/items/{itemId}");
        Assert.Null(after!.FeedStageId);
    }

    [Fact]
    public async Task SetFeedStage_UnknownStageGuid_Returns404()
    {
        var itemId = await CreateItemAsync("Stage-Unknown", ItemCategory.Feed, "kg");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/inventory/items/{itemId}/feed-stage",
            new { feedStageId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetFeedStage_OnNonFeedItem_Returns400()
    {
        var itemId = await CreateItemAsync("Stage-NonFeed", ItemCategory.Medicine, "ml");
        var starterId = await GetFeedStageIdByKeyAsync("starter");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/inventory/items/{itemId}/feed-stage",
            new { feedStageId = starterId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateFeedStage_ValidData_IsListedInFeedStages()
    {
        var key = $"custom_{Guid.NewGuid():N}"[..20];

        var createResponse = await _client.PostAsJsonAsync("/api/v1/inventory/feed-stages", new
        {
            key,
            labelEs = "Etapa personalizada",
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedId>();

        var listResponse = await _client.GetAsync("/api/v1/inventory/feed-stages?includeInactive=true");
        listResponse.EnsureSuccessStatusCode();
        var stages = await listResponse.Content.ReadFromJsonAsync<List<FeedStageDto>>();

        Assert.Contains(stages!, s => s.Id == created!.Id);
    }

    [Fact]
    public async Task CreateFeedStage_InvalidKey_Returns400()
    {
        // Spaces are not allowed in the wire-format key (FeedStage.Create rejects them).
        var response = await _client.PostAsJsonAsync("/api/v1/inventory/feed-stages", new
        {
            key = "has spaces",
            labelEs = "Etiqueta",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeactivateFeedStage_ActiveStage_MarksInactive()
    {
        var key = $"temp_{Guid.NewGuid():N}"[..14];
        var createResponse = await _client.PostAsJsonAsync("/api/v1/inventory/feed-stages", new
        {
            key,
            labelEs = "Temporal",
        });
        createResponse.EnsureSuccessStatusCode();
        var id = (await createResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var deactResponse = await _client.PostAsync($"/api/v1/inventory/feed-stages/{id}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.NoContent, deactResponse.StatusCode);

        var listResponse = await _client.GetAsync("/api/v1/inventory/feed-stages?includeInactive=true");
        listResponse.EnsureSuccessStatusCode();
        var stages = await listResponse.Content.ReadFromJsonAsync<List<FeedStageDto>>();
        var stage = stages!.Single(s => s.Id == id);

        Assert.False(stage.IsActive);
    }

    [Fact]
    public async Task DeactivateFeedStage_AlreadyInactive_Returns400()
    {
        var key = $"temp2_{Guid.NewGuid():N}"[..14];
        var createResponse = await _client.PostAsJsonAsync("/api/v1/inventory/feed-stages", new
        {
            key,
            labelEs = "Ya inactiva",
        });
        createResponse.EnsureSuccessStatusCode();
        var id = (await createResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        await _client.PostAsync($"/api/v1/inventory/feed-stages/{id}/deactivate", content: null);

        // Idempotency: a second deactivate must be rejected by the domain, surfaced as 400.
        var second = await _client.PostAsync($"/api/v1/inventory/feed-stages/{id}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task ActivateFeedStage_InactiveStage_MarksActive()
    {
        var key = $"temp3_{Guid.NewGuid():N}"[..14];
        var createResponse = await _client.PostAsJsonAsync("/api/v1/inventory/feed-stages", new
        {
            key,
            labelEs = "Para reactivar",
        });
        createResponse.EnsureSuccessStatusCode();
        var id = (await createResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        await _client.PostAsync($"/api/v1/inventory/feed-stages/{id}/deactivate", content: null);
        var actResponse = await _client.PostAsync($"/api/v1/inventory/feed-stages/{id}/activate", content: null);

        Assert.Equal(HttpStatusCode.NoContent, actResponse.StatusCode);

        var list = (await (await _client.GetAsync("/api/v1/inventory/feed-stages?includeInactive=true")).Content
            .ReadFromJsonAsync<List<FeedStageDto>>())!;
        Assert.True(list.Single(s => s.Id == id).IsActive);
    }

    // ---- helpers ----

    private async Task<Guid> CreateItemAsync(
        string name, ItemCategory category, string unit, Guid? feedStageId = null)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = $"{name}-{Guid.NewGuid():N}",
            category = category.ToString(),
            unit,
            feedStageId,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private async Task CreateBatchAsync(Guid itemId, string batchNumber, decimal quantity)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/inventory/items/{itemId}/batches",
            new { batchNumber, quantity, costPerUnit = 1m });
        response.EnsureSuccessStatusCode();
    }

    private async Task RegisterConversionAsync(Guid itemId, string fromUnit, string toUnit, decimal factor)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/inventory/items/{itemId}/unit-conversions",
            new { fromUnit, toUnit, factor });
        response.EnsureSuccessStatusCode();
    }

    private async Task<Guid> GetFeedStageIdByKeyAsync(string key)
    {
        var response = await _client.GetAsync("/api/v1/inventory/feed-stages");
        response.EnsureSuccessStatusCode();
        var stages = await response.Content.ReadFromJsonAsync<List<FeedStageDto>>();
        return stages!.Single(s => s.Key == key).Id;
    }

    private sealed record CreatedId(Guid Id);

    private sealed record UnitConversionDto(Guid Id, string FromUnit, string ToUnit, decimal Factor);

    private sealed record InventoryItemDetailDto(
        Guid Id, string Name, ItemCategory Category, string Unit, decimal MinStock, string? Description, Guid? FeedStageId);
}
