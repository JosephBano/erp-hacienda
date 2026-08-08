using System.Net;
using System.Net.Http.Json;
using Hato.Api.Endpoints;
using Hato.Modules.Livestock.Application.HealthPlans;
using Hato.Modules.Livestock.Domain;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// 3.5b.1 (ADR-0016): minimal smoke of the HealthPlan CRUD plus the critical
/// regression that the FK on animal_events.health_plan_item_id is enforced
/// and that a valid id is accepted. The full cronogram resolution and
/// assignment queries are exercised by their own tests (3.5b.2 onwards).
/// </summary>
public class HealthPlanApiTests(HatoApiFactory factory) : IClassFixture<HatoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_Plan_ThenAddItems_Persists()
    {
        var speciesId = await CreateSpeciesAsync();

        var createPlan = await _client.PostAsJsonAsync("/api/v1/health-plans",
            new CreateHealthPlanRequest("Plan de engorde", speciesId));
        createPlan.EnsureSuccessStatusCode();
        var planId = (await createPlan.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var addItem = await _client.PostAsJsonAsync($"/api/v1/health-plans/{planId}/items",
            new AddHealthPlanItemRequest(
                Name: "Vacuna 1",
                EventType: "Vaccination",
                Anchor: PlanAnchor.Birth,
                AnchorOffsetDays: 30,
                ComplianceWindowDays: 3));
        addItem.EnsureSuccessStatusCode();

        var list = await _client.GetAsync("/api/v1/health-plans?speciesId=" + speciesId);
        list.EnsureSuccessStatusCode();
        var plans = await list.Content.ReadFromJsonAsync<List<HealthPlanDto>>();

        Assert.Single(plans!);
        Assert.Equal("Plan de engorde", plans![0].Name);
        Assert.Single(plans[0].Items);
        Assert.Equal("Vacuna 1", plans[0].Items[0].Name);
    }

    [Fact]
    public async Task Create_DuplicatePlan_ReturnsConflict()
    {
        var speciesId = await CreateSpeciesAsync();

        var first = await _client.PostAsJsonAsync("/api/v1/health-plans",
            new CreateHealthPlanRequest("Plan único", speciesId));
        first.EnsureSuccessStatusCode();

        var duplicate = await _client.PostAsJsonAsync("/api/v1/health-plans",
            new CreateHealthPlanRequest("Plan único", speciesId));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task RecordAnimalEvent_WithInvalidHealthPlanItemId_IsRejected()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/animals/{animalId}/events",
            new
            {
                EventType = EventType.Treatment,
                OccurredAt = DateTimeOffset.UtcNow,
                RecordedBy = "field-app",
                PayloadJson = "{}",
                HealthPlanItemId = Guid.NewGuid(),  // valid GUID, but no row exists
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordAnimalEvent_WithValidHealthPlanItemId_IsAccepted()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);

        var planResponse = await _client.PostAsJsonAsync("/api/v1/health-plans",
            new CreateHealthPlanRequest("Plan", speciesId));
        planResponse.EnsureSuccessStatusCode();
        var planId = (await planResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var itemResponse = await _client.PostAsJsonAsync($"/api/v1/health-plans/{planId}/items",
            new AddHealthPlanItemRequest(
                Name: "Vacuna 1",
                EventType: "Vaccination",
                Anchor: PlanAnchor.Birth,
                AnchorOffsetDays: 30,
                ComplianceWindowDays: 3));
        itemResponse.EnsureSuccessStatusCode();
        var itemId = (await itemResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/animals/{animalId}/events",
            new
            {
                EventType = EventType.Treatment,
                OccurredAt = DateTimeOffset.UtcNow,
                RecordedBy = "field-app",
                PayloadJson = "{}",
                HealthPlanItemId = itemId,
            });

        response.EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreateSpeciesAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/species",
            new { name = $"Esp-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private async Task<Guid> CreateAnimalAsync(Guid speciesId)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/animals", new
        {
            speciesId,
            sex = Sex.Female,
            birthDate = (DateOnly?)null,
            breedId = (Guid?)null,
            categoryId = (Guid?)null,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private sealed record CreatedId(Guid Id);
}
