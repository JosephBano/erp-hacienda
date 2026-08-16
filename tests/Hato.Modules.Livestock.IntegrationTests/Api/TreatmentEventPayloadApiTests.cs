using System.Net;
using System.Net.Http.Json;
using Hato.Modules.Livestock.Application.Events;
using Hato.Modules.Livestock.Application.TreatmentCatalogs.AdministrationRoutes;
using Hato.Modules.Livestock.Application.TreatmentCatalogs.TreatmentReasons;
using Hato.Modules.Livestock.Domain;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// 3.5a.2-A second half: the structured treatment payload (route + reason +
/// batch + applied_by + forward-compat health_plan_item_id) round-trips
/// through the API and survives a read-back. docs/planes/sub_planes/PLAN-FASE-3-5-PORCINO-3.5a.2-A.md
/// sec."Pruebas" punto 8 ("se guarda el dato").
/// </summary>
public class TreatmentEventPayloadApiTests(HatoApiFactory factory) : IClassFixture<HatoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> CreateAnimalAsync()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species",
            new { name = $"Porcino-{Guid.NewGuid():N}" });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var animalResponse = await _client.PostAsJsonAsync("/api/v1/animals", new
        {
            speciesId,
            sex = Sex.Female,
            birthDate = (DateOnly?)null,
            breedId = (Guid?)null,
            categoryId = (Guid?)null,
        });
        animalResponse.EnsureSuccessStatusCode();
        return (await animalResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private async Task<Guid> ActiveRouteAsync()
    {
        var response = await _client.GetAsync("/api/v1/administration-routes");
        response.EnsureSuccessStatusCode();
        var routes = await response.Content.ReadFromJsonAsync<List<AdministrationRouteDto>>();
        return routes!.First(r => r.Key == "im").Id;
    }

    [Fact]
    public async Task RecordTreatmentEvent_WithRouteAndReason_PersistsAndRoundTrips()
    {
        var animalId = await CreateAnimalAsync();
        var routeId = await ActiveRouteAsync();

        var create = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Treatment,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "operario",
            payloadJson = "{}",
            routeId,
            reason = "curative",
        });
        create.EnsureSuccessStatusCode();

        var list = await _client.GetAsync($"/api/v1/animals/{animalId}/events");
        list.EnsureSuccessStatusCode();
        var events = await list.Content.ReadFromJsonAsync<List<AnimalEventDto>>();

        var evt = Assert.Single(events!);
        Assert.Equal(routeId, evt.RouteId);
        Assert.Equal("curative", evt.Reason);
    }

    [Fact]
    public async Task RecordTreatmentEvent_WithInactiveRoute_IsRejected()
    {
        var animalId = await CreateAnimalAsync();

        // Create + deactivate a route, then try to use it. The handler
        // mirrors the existing mortality-cause check (Art. 1: row stays
        // in the DB; new writes against it are rejected with a 400).
        var create = await _client.PostAsJsonAsync(
            "/api/v1/administration-routes",
            new { key = $"retired_{Guid.NewGuid():N}", labelEs = "Retirada" });
        create.EnsureSuccessStatusCode();
        var routeId = (await create.Content.ReadFromJsonAsync<CreatedId>())!.Id;
        (await _client.DeleteAsync($"/api/v1/administration-routes/{routeId}")).EnsureSuccessStatusCode();

        var treatment = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Treatment,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "operario",
            payloadJson = "{}",
            routeId,
            reason = "curative",
        });

        Assert.Equal(HttpStatusCode.BadRequest, treatment.StatusCode);
    }

    [Fact]
    public async Task RecordTreatmentEvent_WithUnknownReason_IsRejected()
    {
        var animalId = await CreateAnimalAsync();
        var routeId = await ActiveRouteAsync();

        // "experimental" isn't in the catalogue. The domain rejects with a
        // Problem Details before the request touches the database.
        var treatment = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Treatment,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "operario",
            payloadJson = "{}",
            routeId,
            reason = "experimental",
        });

        Assert.Equal(HttpStatusCode.BadRequest, treatment.StatusCode);
    }

    [Fact]
    public async Task RecordTreatmentEvent_WithCurativeReasonAndMilkWithdrawal_StillCreatesWithdrawalPeriod()
    {
        // The withdrawal-period machinery predates 3.5a.2-A and is independent
        // of the structured treatment payload. Verify both layers cooperate:
        // a Treatment event with reason=curative + milkWithdrawalDays=7
        // creates a WithdrawalPeriod exactly as before.
        var animalId = await CreateAnimalAsync();
        var routeId = await ActiveRouteAsync();

        var treatment = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Treatment,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "operario",
            payloadJson = "{}",
            routeId,
            reason = "curative",
            milkWithdrawalDays = 7,
        });
        treatment.EnsureSuccessStatusCode();

        var withdrawals = await _client.GetAsync($"/api/v1/animals/{animalId}/withdrawal-periods");
        withdrawals.EnsureSuccessStatusCode();
        var list = await withdrawals.Content.ReadFromJsonAsync<List<WithdrawalPeriodDto>>();

        Assert.NotNull(list);
        var w = Assert.Single(list!);
        Assert.Equal(WithdrawalTarget.Milk, w.Target);
    }

    [Fact]
    public async Task RecordTreatmentEvent_WithAppliedByDifferentFromRecordedBy_PersistsBoth()
    {
        var animalId = await CreateAnimalAsync();
        var routeId = await ActiveRouteAsync();
        var recordedById = Guid.NewGuid();
        var appliedById = Guid.NewGuid();

        var treatment = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Treatment,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "operario que tipeó",
            payloadJson = "{}",
            routeId,
            reason = "curative",
            recordedById,
            appliedByUserId = appliedById,
        });
        treatment.EnsureSuccessStatusCode();

        var events = await (await _client.GetAsync($"/api/v1/animals/{animalId}/events"))
            .Content.ReadFromJsonAsync<List<AnimalEventDto>>();
        var evt = Assert.Single(events!);
        Assert.Equal(appliedById, evt.AppliedByUserId);
        Assert.Equal(recordedById, evt.RecordedById);
    }

    [Fact]
    public async Task RecordGroupTreatmentEvent_AcceptsStructuredPayload()
    {
        // Treatments on a lot share the same payload shape as on an animal
        // (ADR-0015: subject changes, the rest is the same).
        var routeId = await ActiveRouteAsync();

        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species",
            new { name = $"Porcino-{Guid.NewGuid():N}" });
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var groupResponse = await _client.PostAsJsonAsync("/api/v1/animal-groups", new
        {
            name = $"Engorde-{Guid.NewGuid():N}",
            description = (string?)null,
            speciesId,
            trackingMode = TrackingMode.Headcount,
        });
        groupResponse.EnsureSuccessStatusCode();
        var groupId = (await groupResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var treatment = await _client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/events", new
        {
            eventType = EventType.Treatment,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "operario",
            payloadJson = "{}",
            affectedCount = 12,
            routeId,
            reason = "scheduled",
        });
        treatment.EnsureSuccessStatusCode();

        var events = await (await _client.GetAsync($"/api/v1/animal-groups/{groupId}/events"))
            .Content.ReadFromJsonAsync<List<AnimalEventDto>>();
        var evt = Assert.Single(events!);
        Assert.Equal(routeId, evt.RouteId);
        Assert.Equal("scheduled", evt.Reason);
        Assert.Equal(12, evt.AffectedCount);
    }

    [Fact]
    public async Task RecordTreatmentEvent_WithoutRouteOrReason_IsLegal()
    {
        // Backward compat: legacy callers that only send the old fields keep
        // working. The new fields are nullable.
        var animalId = await CreateAnimalAsync();

        var treatment = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Treatment,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "legacy",
            payloadJson = "{\"dose\":\"5ml\"}",
        });
        treatment.EnsureSuccessStatusCode();

        var events = await (await _client.GetAsync($"/api/v1/animals/{animalId}/events"))
            .Content.ReadFromJsonAsync<List<AnimalEventDto>>();
        var evt = Assert.Single(events!);
        Assert.Null(evt.RouteId);
        Assert.Null(evt.Reason);
    }

    private sealed record CreatedId(Guid Id);
}