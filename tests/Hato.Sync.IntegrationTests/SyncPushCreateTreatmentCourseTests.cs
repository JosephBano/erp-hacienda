using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// docs/planes/fase-3-5/spec-3.5a.md sec.3.5a.2-C: the heart of this sub-branch. Before this
/// change, `TreatmentFormScreen` (dead code, never wired into navigation)
/// enqueued `recordAnimalEvent` with `reasonId` (a catalog row id) and
/// `doseKg` — neither of which `RecordAnimalEventCommand` declares. Because
/// the push deserializer had no `UnmappedMemberHandling.Disallow`, the server
/// silently dropped both fields and still answered "Accepted": the same
/// failure shape that produced a false-positive close in an earlier phase (a
/// discarded `motherId`, no error, no signal — see AGENTS.md / the sub-plan).
///
/// `VaccinateScreen` and `TreatScreen` (3.5a.2-C) instead push
/// <c>createTreatmentCourse</c>, whose payload the test below builds with the
/// *exact* field names <c>EventService.recordTreatmentCourse</c>
/// (clients/field-app/src/services/eventService.ts) sends — camelCase
/// mirrors of <see cref="Hato.Modules.Livestock.Application.TreatmentCourses.CreateTreatmentCourseCommand"/>'s
/// parameters, nothing more. Because
/// <c>PushSyncCommands.JsonOptions</c> now runs with
/// <c>UnmappedMemberHandling.Disallow</c>, an unrecognised field on this
/// payload would fail the push with a 400-shaped rejection instead of being
/// dropped in silence — this test proves the current mobile payload does NOT
/// trigger that failure and that every field it sends is actually persisted.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncPushCreateTreatmentCourseTests(SyncApiFactory factory)
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

    private async Task<Guid> DoseKindIdAsync(SyncTestContext context, string key)
    {
        var response = await context.Client.GetAsync("/api/v1/dose-kinds");
        response.EnsureSuccessStatusCode();
        var kinds = await response.Content.ReadFromJsonAsync<JsonElement>();
        return kinds.EnumerateArray().Single(k => k.GetProperty("key").GetString() == key).GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Builds exactly the payload `VaccinateScreen` produces: reason fixed to
    /// `scheduled`, dose kind `per_head`, factor amount 1, unit taken from the
    /// product (never typed by the operator — "la unidad es del producto, no
    /// del operario").
    /// </summary>
    [Fact]
    public async Task Push_VaccinateScreenPayload_CreatesATreatmentCourseWithThePerHeadDose()
    {
        var context = await SyncTestContext.CreateAsync(factory, "vaccinate-payload");
        var speciesId = await context.CreateSpeciesAsync("Porcino");
        var animalId = await context.CreateAnimalAsync(speciesId);
        var routeId = await CreateAdministrationRouteAsync(context);
        var perHeadId = await DoseKindIdAsync(context, "per_head");

        // This is the literal shape EventService.recordTreatmentCourse enqueues
        // for the outbox entry 'createTreatmentCourse' when VaccinateScreen
        // confirms — see the JSDoc on TreatmentCourseInput for why the field
        // names cannot drift from this without a loud failure now.
        var result = await context.PushAsync("createTreatmentCourse", new
        {
            animalId,
            startsAt = DateTimeOffset.UtcNow,
            routeId,
            reason = "scheduled",
            productId = (Guid?)null,
            doseKindId = perHeadId,
            doseFactorAmount = 1,
            doseFactorUnit = "dosis",
            notes = "Vacunación: Triple porcina.",
        });

        Assert.Equal("Accepted", result.GetProperty("status").GetString());
        var courseId = Guid.Parse(result.GetProperty("resultRef").GetString()!);

        var coursesResponse = await context.Client.GetAsync($"/api/v1/animals/{animalId}/treatment-courses");
        coursesResponse.EnsureSuccessStatusCode();
        var courses = (await coursesResponse.Content.ReadFromJsonAsync<JsonElement>())!.EnumerateArray().ToList();
        var course = Assert.Single(courses, c => c.GetProperty("id").GetGuid() == courseId);

        Assert.Equal(routeId, course.GetProperty("routeId").GetGuid());
        Assert.Equal("scheduled", course.GetProperty("reason").GetString());
        Assert.Equal(perHeadId, course.GetProperty("doseKindId").GetGuid());
        Assert.Equal(1m, course.GetProperty("doseFactorAmount").GetDecimal());
        Assert.Equal("dosis", course.GetProperty("doseFactorUnit").GetString());
        Assert.False(course.GetProperty("isSynthetic").GetBoolean());

        var application = Assert.Single(course.GetProperty("applications").EnumerateArray());
        Assert.False(application.GetProperty("isPlausibilityConfirmed").GetBoolean());
    }

    /// <summary>
    /// Builds exactly the payload `TreatScreen` produces after the operator
    /// confirmed an improbable dose: `isPlausibilityConfirmed = true` must
    /// survive the push and land on the first application, not be dropped.
    /// </summary>
    [Fact]
    public async Task Push_TreatScreenPayload_PersistsTheAdministeredDoseAndThePlausibilityConfirmation()
    {
        var context = await SyncTestContext.CreateAsync(factory, "treat-payload");
        var speciesId = await context.CreateSpeciesAsync("Porcino");
        var animalId = await context.CreateAnimalAsync(speciesId);
        var routeId = await CreateAdministrationRouteAsync(context);
        var absoluteId = await DoseKindIdAsync(context, "absolute");

        var result = await context.PushAsync("createTreatmentCourse", new
        {
            animalId,
            startsAt = DateTimeOffset.UtcNow,
            routeId,
            reason = "curative",
            productId = (Guid?)null,
            doseKindId = absoluteId,
            doseFactorAmount = 12.5m,
            doseFactorUnit = "ml",
            notes = (string?)null,
            administeredDoseAmount = 12.5m,
            administeredDoseUnit = "ml",
            applicationNotes = "Cojera pata trasera derecha.",
            isPlausibilityConfirmed = true,
        });

        Assert.Equal("Accepted", result.GetProperty("status").GetString());
        var courseId = Guid.Parse(result.GetProperty("resultRef").GetString()!);

        var coursesResponse = await context.Client.GetAsync($"/api/v1/animals/{animalId}/treatment-courses");
        coursesResponse.EnsureSuccessStatusCode();
        var courses = (await coursesResponse.Content.ReadFromJsonAsync<JsonElement>())!.EnumerateArray().ToList();
        var course = Assert.Single(courses, c => c.GetProperty("id").GetGuid() == courseId);

        Assert.Equal("curative", course.GetProperty("reason").GetString());

        var application = Assert.Single(course.GetProperty("applications").EnumerateArray());
        Assert.Equal(12.5m, application.GetProperty("administeredDoseAmount").GetDecimal());
        Assert.Equal("ml", application.GetProperty("administeredDoseUnit").GetString());
        Assert.True(application.GetProperty("isPlausibilityConfirmed").GetBoolean());
    }

    /// <summary>
    /// The regression this whole test file guards against: a payload shaped
    /// like the OLD, never-wired `TreatmentFormScreen` sent (`reasonId` +
    /// `doseKg` instead of `reason` + the dose-course fields) must now be
    /// **rejected loudly**, not accepted with the extra fields silently
    /// dropped. Proves `UnmappedMemberHandling.Disallow` actually does its
    /// job on this path.
    /// </summary>
    [Fact]
    public async Task Push_ThePreviousDeadScreensShapeWithReasonIdAndDoseKg_IsRejectedNotSilentlyAccepted()
    {
        var context = await SyncTestContext.CreateAsync(factory, "treat-old-shape-rejected");
        var speciesId = await context.CreateSpeciesAsync("Porcino");
        var animalId = await context.CreateAnimalAsync(speciesId);
        var routeId = await CreateAdministrationRouteAsync(context);

        var result = await context.PushAsync("recordAnimalEvent", new
        {
            animalId,
            eventType = "Treatment",
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "field-app",
            payloadJson = "{}",
            routeId,
            reasonId = Guid.NewGuid(), // the old, wrong field: a catalog row id, not the wire-format key
            doseKg = "10", // the old, wrong field: RecordAnimalEventCommand has no such member
        });

        Assert.Equal("Rejected", result.GetProperty("status").GetString());
        Assert.NotNull(result.GetProperty("errorDetails").GetString());
    }
}
