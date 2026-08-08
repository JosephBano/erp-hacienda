using System.Net;
using System.Net.Http.Json;
using Hato.Api.Endpoints;
using Hato.Modules.Livestock.Application.PlausibilityRanges;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// 3.5a.6 (ADR-0022): the plausibility range catalog is the foundation of
/// field-level validation. The seeded defaults cover the standard species and
/// magnitudes; the panel can refine them. The evaluator endpoint is the
/// reference implementation of the fail-open contract used by the mobile
/// validator when the local table is empty.
/// </summary>
public class PlausibilityRangeApiTests(HatoApiFactory factory) : IClassFixture<HatoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Catalog_AfterMigration_IsSeededWithTheDefaultRanges()
    {
        var response = await _client.GetAsync("/api/v1/plausibility-ranges");
        response.EnsureSuccessStatusCode();
        var ranges = await response.Content.ReadFromJsonAsync<List<PlausibilityRangeDto>>();

        Assert.NotNull(ranges);
        Assert.NotEmpty(ranges!);

        // The seed includes weight_kg for the three foundational species and
        // milk_liters for the two milkable species.
        var magnitudes = ranges!.Select(r => r.Magnitude).Distinct().ToList();
        Assert.Contains("weight_kg", magnitudes);
        Assert.Contains("milk_liters", magnitudes);
    }

    [Fact]
    public async Task Create_ThenDeactivate_HidesItFromTheDefaultList()
    {
        // Create a species first (the seed ships with stable ids, but for a new
        // range we need a real species reference).
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Especie-{Guid.NewGuid():N}",
        });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var createRequest = new CreatePlausibilityRangeRequest(
            speciesId, null, "weight_kg", 10m, 200m, 1m, 400m);
        var createResponse = await _client.PostAsJsonAsync("/api/v1/plausibility-ranges", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var rangeId = (await createResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var deactivateResponse = await _client.DeleteAsync($"/api/v1/plausibility-ranges/{rangeId}");
        deactivateResponse.EnsureSuccessStatusCode();

        var listResponse = await _client.GetAsync("/api/v1/plausibility-ranges?speciesId=" + speciesId);
        var ranges = await listResponse.Content.ReadFromJsonAsync<List<PlausibilityRangeDto>>();

        Assert.DoesNotContain(ranges!, r => r.Id == rangeId);

        // But includeInactive=true still returns it.
        var includeInactive = await _client.GetAsync(
            $"/api/v1/plausibility-ranges?speciesId={speciesId}&includeInactive=true");
        var allRanges = await includeInactive.Content.ReadFromJsonAsync<List<PlausibilityRangeDto>>();
        Assert.Contains(allRanges!, r => r.Id == rangeId);
    }

    [Fact]
    public async Task Create_WithDuplicateCombination_ReturnsConflict()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Especie-{Guid.NewGuid():N}",
        });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var first = await _client.PostAsJsonAsync("/api/v1/plausibility-ranges",
            new CreatePlausibilityRangeRequest(speciesId, null, "weight_kg", 10m, 200m, 1m, 400m));
        first.EnsureSuccessStatusCode();

        var duplicate = await _client.PostAsJsonAsync("/api/v1/plausibility-ranges",
            new CreatePlausibilityRangeRequest(speciesId, null, "weight_kg", 20m, 300m, 5m, 500m));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidMagnitude_ReturnsBadRequest()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Especie-{Guid.NewGuid():N}",
        });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var response = await _client.PostAsJsonAsync("/api/v1/plausibility-ranges",
            new CreatePlausibilityRangeRequest(speciesId, null, "Weight_Kg", 10m, 200m, 1m, 400m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvertedBounds_ReturnsBadRequest()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Especie-{Guid.NewGuid():N}",
        });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var response = await _client.PostAsJsonAsync("/api/v1/plausibility-ranges",
            new CreatePlausibilityRangeRequest(speciesId, null, "weight_kg", 200m, 10m, 1m, 400m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_WithinPlausible_ReturnsPass()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Especie-{Guid.NewGuid():N}",
        });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        await _client.PostAsJsonAsync("/api/v1/plausibility-ranges",
            new CreatePlausibilityRangeRequest(speciesId, null, "weight_kg", 10m, 200m, 1m, 400m));

        var response = await _client.PostAsJsonAsync("/api/v1/plausibility-ranges/evaluate",
            new EvaluatePlausibilityRequest(speciesId, null, "weight_kg", 50m));
        response.EnsureSuccessStatusCode();
        var verdict = await response.Content.ReadFromJsonAsync<VerdictDto>();

        Assert.Equal("Pass", verdict!.Verdict);
    }

    [Fact]
    public async Task Evaluate_OutsidePlausibleButInsideAbsolute_ReturnsConfirm()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Especie-{Guid.NewGuid():N}",
        });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        await _client.PostAsJsonAsync("/api/v1/plausibility-ranges",
            new CreatePlausibilityRangeRequest(speciesId, null, "weight_kg", 10m, 200m, 1m, 400m));

        var response = await _client.PostAsJsonAsync("/api/v1/plausibility-ranges/evaluate",
            new EvaluatePlausibilityRequest(speciesId, null, "weight_kg", 350m));
        response.EnsureSuccessStatusCode();
        var verdict = await response.Content.ReadFromJsonAsync<VerdictDto>();

        Assert.Equal("Confirm", verdict!.Verdict);
    }

    [Fact]
    public async Task Evaluate_OutsideAbsolute_ReturnsBlock()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Especie-{Guid.NewGuid():N}",
        });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        await _client.PostAsJsonAsync("/api/v1/plausibility-ranges",
            new CreatePlausibilityRangeRequest(speciesId, null, "weight_kg", 10m, 200m, 1m, 400m));

        var response = await _client.PostAsJsonAsync("/api/v1/plausibility-ranges/evaluate",
            new EvaluatePlausibilityRequest(speciesId, null, "weight_kg", 500m));
        response.EnsureSuccessStatusCode();
        var verdict = await response.Content.ReadFromJsonAsync<VerdictDto>();

        Assert.Equal("Block", verdict!.Verdict);
    }

    [Fact]
    public async Task Evaluate_NoRangeForCombination_ReturnsPass_FailOpen()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Especie-{Guid.NewGuid():N}",
        });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        // No range created. Any value, even "1000 L", should pass — fail-open.
        var response = await _client.PostAsJsonAsync("/api/v1/plausibility-ranges/evaluate",
            new EvaluatePlausibilityRequest(speciesId, null, "weight_kg", 1000m));
        response.EnsureSuccessStatusCode();
        var verdict = await response.Content.ReadFromJsonAsync<VerdictDto>();

        Assert.Equal("Pass", verdict!.Verdict);
    }

    [Fact]
    public async Task Evaluate_WithCategorySpecificRange_PrefersCategoryOverSpecies()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Especie-{Guid.NewGuid():N}",
        });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var categoryResponse = await _client.PostAsJsonAsync("/api/v1/animal-categories",
            new { speciesId, name = $"Cat-{Guid.NewGuid():N}" });
        categoryResponse.EnsureSuccessStatusCode();
        var categoryId = (await categoryResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        // Species-wide range: 0–80 plausible, 0–100 absolute.
        await _client.PostAsJsonAsync("/api/v1/plausibility-ranges",
            new CreatePlausibilityRangeRequest(speciesId, null, "weight_kg", 0m, 80m, 0m, 100m));

        // Category-specific range: 30–50 plausible, 0–100 absolute.
        await _client.PostAsJsonAsync("/api/v1/plausibility-ranges",
            new CreatePlausibilityRangeRequest(speciesId, categoryId, "weight_kg", 30m, 50m, 0m, 100m));

        // Value 25 lies in the species-wide plausible range but outside the
        // category-specific plausible range. The category-specific range wins.
        var withCategory = await _client.PostAsJsonAsync("/api/v1/plausibility-ranges/evaluate",
            new EvaluatePlausibilityRequest(speciesId, categoryId, "weight_kg", 25m));
        withCategory.EnsureSuccessStatusCode();
        var categoryVerdict = await withCategory.Content.ReadFromJsonAsync<VerdictDto>();
        Assert.Equal("Confirm", categoryVerdict!.Verdict);

        // Same value, but no category specified → species-wide range applies → Pass.
        var withoutCategory = await _client.PostAsJsonAsync("/api/v1/plausibility-ranges/evaluate",
            new EvaluatePlausibilityRequest(speciesId, null, "weight_kg", 25m));
        withoutCategory.EnsureSuccessStatusCode();
        var speciesVerdict = await withoutCategory.Content.ReadFromJsonAsync<VerdictDto>();
        Assert.Equal("Pass", speciesVerdict!.Verdict);
    }

    private sealed record CreatedId(Guid Id);
    private sealed record VerdictDto(string Verdict);
}
