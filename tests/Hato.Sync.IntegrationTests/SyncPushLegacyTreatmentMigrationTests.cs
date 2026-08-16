using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.2-B task 9 / test 10: a device with a legacy
/// <c>treatment</c> event (a free-text <c>dose</c> in its payload, from before
/// <c>TreatmentCourse</c> existed) syncs through the exact same
/// <c>recordAnimalEvent</c> push path every other event uses. The handler
/// synthesises a one-application course and marks the original event
/// <c>migratedToCourseId</c>. Retrying the push (the same
/// <c>clientOperationId</c>, exactly what a dropped connection produces) must
/// not create a second course — which the generic push dedup
/// (<c>SyncPushProtocolTests</c>) already guarantees, so this test proves the
/// migration rides on top of it rather than re-deriving idempotency itself.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncPushLegacyTreatmentMigrationTests(SyncApiFactory factory)
{
    private async Task<Guid> CreateAdministrationRouteAsync(SyncTestContext context)
    {
        var response = await context.Client.PostAsJsonAsync("/api/v1/administration-routes", new
        {
            key = $"im_{Guid.NewGuid():N}"[..20],
            labelEs = "Intramuscular",
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Push_LegacyTreatmentWithFreeDose_CreatesSyntheticTreatmentCourse()
    {
        var context = await SyncTestContext.CreateAsync(factory, "legacy-dose-migration");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId);
        var routeId = await CreateAdministrationRouteAsync(context);

        var result = await context.PushAsync("recordAnimalEvent", new
        {
            animalId,
            eventType = "Treatment",
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "empleado-campo",
            payloadJson = "{\"dose\":10,\"unit\":\"ml\"}",
            routeId,
        });

        Assert.Equal("Accepted", result.GetProperty("status").GetString());
        var eventId = Guid.Parse(result.GetProperty("resultRef").GetString()!);

        var eventsResponse = await context.Client.GetAsync($"/api/v1/animals/{animalId}/events");
        eventsResponse.EnsureSuccessStatusCode();
        var events = (await eventsResponse.Content.ReadFromJsonAsync<JsonElement>())!.EnumerateArray().ToList();
        var migratedEvent = events.Single(e => e.GetProperty("id").GetGuid() == eventId);

        Assert.True(migratedEvent.TryGetProperty("migratedToCourseId", out var courseIdProp));
        Assert.NotEqual(JsonValueKind.Null, courseIdProp.ValueKind);

        var coursesResponse = await context.Client.GetAsync($"/api/v1/animals/{animalId}/treatment-courses");
        coursesResponse.EnsureSuccessStatusCode();
        var courses = (await coursesResponse.Content.ReadFromJsonAsync<JsonElement>())!.EnumerateArray().ToList();

        var course = Assert.Single(courses);
        Assert.True(course.GetProperty("isSynthetic").GetBoolean());
        var applications = course.GetProperty("applications").EnumerateArray().ToList();
        var application = Assert.Single(applications);
        Assert.Equal(10m, application.GetProperty("administeredDoseAmount").GetDecimal());
        Assert.Equal("ml", application.GetProperty("administeredDoseUnit").GetString());
    }

    [Fact]
    public async Task Push_SameLegacyTreatmentTwice_DoesNotDuplicateTheSyntheticCourse()
    {
        var context = await SyncTestContext.CreateAsync(factory, "legacy-dose-migration-retry");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId);
        var routeId = await CreateAdministrationRouteAsync(context);
        var operationId = Guid.NewGuid();

        var payload = new
        {
            animalId,
            eventType = "Treatment",
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "empleado-campo",
            payloadJson = "{\"dose\":10,\"unit\":\"ml\"}",
            routeId,
        };

        var first = await context.PushAsync("recordAnimalEvent", payload, operationId);
        var second = await context.PushAsync("recordAnimalEvent", payload, operationId);

        Assert.Equal("Accepted", first.GetProperty("status").GetString());
        Assert.Equal("Duplicate", second.GetProperty("status").GetString());

        var coursesResponse = await context.Client.GetAsync($"/api/v1/animals/{animalId}/treatment-courses");
        coursesResponse.EnsureSuccessStatusCode();
        var courses = (await coursesResponse.Content.ReadFromJsonAsync<JsonElement>())!.EnumerateArray().ToList();

        Assert.Single(courses);
    }
}
