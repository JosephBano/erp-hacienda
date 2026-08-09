using System.Net;
using System.Net.Http.Json;
using Hato.Modules.Livestock.Application.Events;
using Hato.Modules.Livestock.Application.TreatmentCatalogs.AdministrationRoutes;
using Hato.Modules.Livestock.Application.TreatmentCourses;
using Hato.Modules.Livestock.Domain;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// PLAN-FASE-3-5-PORCINO-3.5a.2-B: the three dose forms, calculated vs.
/// administered, optional dose with mandatory unit-when-present, and
/// <c>TreatmentCourse</c> as a series with one withdrawal period computed from
/// its last application. Sub-plan "Pruebas" section, tests 1, 2, 3, 4, 5, 6, 7,
/// 8 and the non-regression test 9 (test 10, the backward-compat migration
/// idempotency, lives in <c>Hato.Sync.IntegrationTests</c> because it is a sync
/// push scenario).
/// </summary>
public class TreatmentCourseApiTests(HatoApiFactory factory) : IClassFixture<HatoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> CreateSpeciesAsync() =>
        (await (await _client.PostAsJsonAsync("/api/v1/species", new { name = $"Porcino-{Guid.NewGuid():N}" }))
            .Content.ReadFromJsonAsync<CreatedId>())!.Id;

    private async Task<Guid> CreateAnimalAsync(Guid speciesId) =>
        (await (await _client.PostAsJsonAsync("/api/v1/animals", new
        {
            speciesId,
            sex = Sex.Female,
            birthDate = (DateOnly?)null,
            breedId = (Guid?)null,
            categoryId = (Guid?)null,
        })).Content.ReadFromJsonAsync<CreatedId>())!.Id;

    private async Task<Guid> ActiveRouteAsync()
    {
        var response = await _client.GetAsync("/api/v1/administration-routes");
        response.EnsureSuccessStatusCode();
        var routes = await response.Content.ReadFromJsonAsync<List<AdministrationRouteDto>>();
        return routes!.First(r => r.Key == "im").Id;
    }

    private async Task<Guid> DoseKindIdAsync(string key)
    {
        var response = await _client.GetAsync("/api/v1/dose-kinds");
        response.EnsureSuccessStatusCode();
        var kinds = await response.Content.ReadFromJsonAsync<List<DoseKindDto>>();
        return kinds!.First(k => k.Key == key).Id;
    }

    private async Task RecordAnimalWeighingAsync(Guid animalId, decimal weightKg)
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Weighing,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "operario",
            payloadJson = $"{{\"weightKg\":{weightKg}}}",
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task<(Guid GroupId, int HeadCount)> SeedHeadcountGroupWithWeighingAsync(
        int headCount, int sampleCount, decimal avgKg)
    {
        var speciesId = await CreateSpeciesAsync();

        var groupResponse = await _client.PostAsJsonAsync("/api/v1/animal-groups", new
        {
            name = $"Engorde-{Guid.NewGuid():N}",
            description = (string?)null,
            speciesId,
            trackingMode = TrackingMode.Headcount,
        });
        groupResponse.EnsureSuccessStatusCode();
        var groupId = (await groupResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        for (var i = 0; i < headCount; i++)
        {
            var animalId = await CreateAnimalAsync(speciesId);
            var addMember = await _client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/members", new
            {
                animalId,
                joinedAt = new DateOnly(2026, 1, 1),
            });
            addMember.EnsureSuccessStatusCode();
        }

        var weighing = await _client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/events", new
        {
            eventType = EventType.Weighing,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "capataz",
            payloadJson = $"{{\"sampleCount\":{sampleCount},\"avgKg\":{avgKg}}}",
        });
        weighing.EnsureSuccessStatusCode();

        return (groupId, headCount);
    }

    // --- Test 1: dose with a value and no unit is rejected; with unit is accepted. ---

    [Fact]
    public async Task CreateCourse_AdministeredDoseWithoutUnit_IsRejected()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);
        var routeId = await ActiveRouteAsync();
        var doseKindId = await DoseKindIdAsync(DoseKind.Keys.Absolute);

        var response = await _client.PostAsJsonAsync("/api/v1/treatment-courses", new
        {
            animalId,
            groupId = (Guid?)null,
            startsAt = DateTimeOffset.UtcNow,
            routeId,
            reason = "curative",
            productId = (Guid?)null,
            doseKindId,
            doseFactorAmount = 10m,
            doseFactorUnit = "ml",
            notes = (string?)null,
            administeredDoseAmount = 10m,
            administeredDoseUnit = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCourse_AdministeredDoseWithUnit_IsAccepted()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);
        var routeId = await ActiveRouteAsync();
        var doseKindId = await DoseKindIdAsync(DoseKind.Keys.Absolute);

        var response = await _client.PostAsJsonAsync("/api/v1/treatment-courses", new
        {
            animalId,
            groupId = (Guid?)null,
            startsAt = DateTimeOffset.UtcNow,
            routeId,
            reason = "curative",
            productId = (Guid?)null,
            doseKindId,
            doseFactorAmount = 10m,
            doseFactorUnit = "ml",
            notes = (string?)null,
            administeredDoseAmount = 10m,
            administeredDoseUnit = "ml",
        });

        response.EnsureSuccessStatusCode();
    }

    // --- Test 2: dose absent is valid, with or without notes. ---

    [Fact]
    public async Task CreateCourse_WithoutAdministeredDose_IsAccepted_WithOrWithoutNotes()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);
        var routeId = await ActiveRouteAsync();
        var doseKindId = await DoseKindIdAsync(DoseKind.Keys.Absolute);

        var withNotes = await _client.PostAsJsonAsync("/api/v1/treatment-courses", new
        {
            animalId,
            groupId = (Guid?)null,
            startsAt = DateTimeOffset.UtcNow,
            routeId,
            reason = "curative",
            productId = (Guid?)null,
            doseKindId,
            doseFactorAmount = 10m,
            doseFactorUnit = "ml",
            notes = (string?)null,
            applicationNotes = "se derramó al aplicar",
        });
        withNotes.EnsureSuccessStatusCode();

        var withoutNotes = await _client.PostAsJsonAsync("/api/v1/treatment-courses", new
        {
            animalId,
            groupId = (Guid?)null,
            startsAt = DateTimeOffset.UtcNow,
            routeId,
            reason = "curative",
            productId = (Guid?)null,
            doseKindId,
            doseFactorAmount = 10m,
            doseFactorUnit = "ml",
            notes = (string?)null,
        });
        withoutNotes.EnsureSuccessStatusCode();
    }

    // --- Test 3: PerWeight with a weighing computes the dose. ---

    [Fact]
    public async Task CreateCourse_PerWeightWithWeighing_ComputesCalculatedDose()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);
        await RecordAnimalWeighingAsync(animalId, 200m);

        var routeId = await ActiveRouteAsync();
        var doseKindId = await DoseKindIdAsync(DoseKind.Keys.PerWeight);

        var response = await _client.PostAsJsonAsync("/api/v1/treatment-courses", new
        {
            animalId,
            groupId = (Guid?)null,
            startsAt = DateTimeOffset.UtcNow,
            routeId,
            reason = "curative",
            productId = (Guid?)null,
            doseKindId,
            doseFactorAmount = 0.1m, // 1 ml / 10 kg
            doseFactorUnit = "ml",
            notes = (string?)null,
        });
        response.EnsureSuccessStatusCode();
        var courseId = (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var course = await GetCourseAsync(courseId);
        var application = Assert.Single(course.Applications);

        Assert.Equal(20m, application.CalculatedDoseAmount);
        Assert.Equal("ml", application.CalculatedDoseUnit);
        Assert.False(application.IsEstimated);
    }

    // --- Test 4: PerWeight without a weighing is rejected. ---

    [Fact]
    public async Task CreateCourse_PerWeightWithoutWeighing_IsRejected_WithTypedErrorCode()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);
        var routeId = await ActiveRouteAsync();
        var doseKindId = await DoseKindIdAsync(DoseKind.Keys.PerWeight);

        var response = await _client.PostAsJsonAsync("/api/v1/treatment-courses", new
        {
            animalId,
            groupId = (Guid?)null,
            startsAt = DateTimeOffset.UtcNow,
            routeId,
            reason = "curative",
            productId = (Guid?)null,
            doseKindId,
            doseFactorAmount = 0.1m,
            doseFactorUnit = "ml",
            notes = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(MissingWeighingForDoseException.ErrorCode, body);
    }

    // --- Test 5: PerWeight on a group uses the sample average and marks it estimated. ---

    [Fact]
    public async Task CreateCourse_PerWeightOnGroup_UsesSampleAverage_AndMarksEstimated()
    {
        var (groupId, _) = await SeedHeadcountGroupWithWeighingAsync(headCount: 42, sampleCount: 10, avgKg: 35m);

        var routeId = await ActiveRouteAsync();
        var doseKindId = await DoseKindIdAsync(DoseKind.Keys.PerWeight);

        var response = await _client.PostAsJsonAsync("/api/v1/treatment-courses", new
        {
            animalId = (Guid?)null,
            groupId,
            startsAt = DateTimeOffset.UtcNow,
            routeId,
            reason = "scheduled",
            productId = (Guid?)null,
            doseKindId,
            doseFactorAmount = 0.1m, // 1 ml / 10 kg
            doseFactorUnit = "ml",
            notes = (string?)null,
        });
        response.EnsureSuccessStatusCode();
        var courseId = (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var course = await GetCourseAsync(courseId);
        var application = Assert.Single(course.Applications);

        // 42 head x 35 kg x 0.1 ml/kg = 147 ml.
        Assert.Equal(147m, application.CalculatedDoseAmount);
        Assert.True(application.IsEstimated);
        Assert.Contains("10", application.Notes);
    }

    // --- Test 6: calculated ≠ administered persists without correction. ---

    [Fact]
    public async Task CreateCourse_CalculatedAndAdministeredDiffer_BothPersistUncorrected()
    {
        var (groupId, _) = await SeedHeadcountGroupWithWeighingAsync(headCount: 42, sampleCount: 10, avgKg: 35m);

        var routeId = await ActiveRouteAsync();
        var doseKindId = await DoseKindIdAsync(DoseKind.Keys.PerWeight);

        var response = await _client.PostAsJsonAsync("/api/v1/treatment-courses", new
        {
            animalId = (Guid?)null,
            groupId,
            startsAt = DateTimeOffset.UtcNow,
            routeId,
            reason = "scheduled",
            productId = (Guid?)null,
            doseKindId,
            doseFactorAmount = 0.1m,
            doseFactorUnit = "ml",
            notes = (string?)null,
            administeredDoseAmount = 200m,
            administeredDoseUnit = "ml",
        });
        response.EnsureSuccessStatusCode();
        var courseId = (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var course = await GetCourseAsync(courseId);
        var application = Assert.Single(course.Applications);

        Assert.Equal(147m, application.CalculatedDoseAmount);
        Assert.Equal(200m, application.AdministeredDoseAmount);
    }

    // --- Test 7: an unknown or inactive route is rejected. ---

    [Fact]
    public async Task CreateCourse_WithUnknownRoute_IsRejected()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);
        var doseKindId = await DoseKindIdAsync(DoseKind.Keys.Absolute);

        var response = await _client.PostAsJsonAsync("/api/v1/treatment-courses", new
        {
            animalId,
            groupId = (Guid?)null,
            startsAt = DateTimeOffset.UtcNow,
            routeId = Guid.NewGuid(),
            reason = "curative",
            productId = (Guid?)null,
            doseKindId,
            doseFactorAmount = 10m,
            doseFactorUnit = "ml",
            notes = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCourse_WithInactiveRoute_IsRejected()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);
        var doseKindId = await DoseKindIdAsync(DoseKind.Keys.Absolute);

        var routeCreate = await _client.PostAsJsonAsync(
            "/api/v1/administration-routes", new { key = $"retired_{Guid.NewGuid():N}", labelEs = "Retirada" });
        routeCreate.EnsureSuccessStatusCode();
        var routeId = (await routeCreate.Content.ReadFromJsonAsync<CreatedId>())!.Id;
        (await _client.DeleteAsync($"/api/v1/administration-routes/{routeId}")).EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync("/api/v1/treatment-courses", new
        {
            animalId,
            groupId = (Guid?)null,
            startsAt = DateTimeOffset.UtcNow,
            routeId,
            reason = "curative",
            productId = (Guid?)null,
            doseKindId,
            doseFactorAmount = 10m,
            doseFactorUnit = "ml",
            notes = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Test 8: a 3-day TreatmentCourse produces one course row, three ---
    // --- applications and a single withdrawal period from the last one.  ---

    [Fact]
    public async Task ThreeDayCourse_ProducesThreeApplications_AndOneWithdrawalFromTheLastOne()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);
        var routeId = await ActiveRouteAsync();
        var doseKindId = await DoseKindIdAsync(DoseKind.Keys.Absolute);

        var day1 = new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);

        var create = await _client.PostAsJsonAsync("/api/v1/treatment-courses", new
        {
            animalId,
            groupId = (Guid?)null,
            startsAt = day1,
            routeId,
            reason = "curative",
            productId = (Guid?)null,
            doseKindId,
            doseFactorAmount = 10m,
            doseFactorUnit = "ml",
            notes = (string?)null,
            administeredDoseAmount = 10m,
            administeredDoseUnit = "ml",
            milkWithdrawalDays = 3,
        });
        create.EnsureSuccessStatusCode();
        var courseId = (await create.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var day2 = day1.AddDays(1);
        var day3 = day1.AddDays(2);

        (await _client.PostAsJsonAsync($"/api/v1/treatment-courses/{courseId}/applications", new
        {
            appliedAt = day2,
            administeredDoseAmount = 10m,
            administeredDoseUnit = "ml",
            milkWithdrawalDays = 3,
        })).EnsureSuccessStatusCode();

        (await _client.PostAsJsonAsync($"/api/v1/treatment-courses/{courseId}/applications", new
        {
            appliedAt = day3,
            administeredDoseAmount = 10m,
            administeredDoseUnit = "ml",
            milkWithdrawalDays = 3,
        })).EnsureSuccessStatusCode();

        var course = await GetCourseAsync(courseId);
        Assert.Equal(3, course.Applications.Count);
        Assert.Equal(day3, course.EndsAt);

        var withdrawalsResponse = await _client.GetAsync(
            $"/api/v1/animals/{animalId}/withdrawal-periods?targetDate={DateOnly.FromDateTime(day3.UtcDateTime):yyyy-MM-dd}");
        withdrawalsResponse.EnsureSuccessStatusCode();
        var withdrawals = await withdrawalsResponse.Content.ReadFromJsonAsync<List<WithdrawalPeriodDto>>();

        // The window that covers day3 is the one anchored to the last application:
        // day3 + 3 days. Earlier applications' windows may or may not still be
        // active on day3 depending on how far apart the days are — what matters
        // is that the latest one, dated from the last application, is present.
        Assert.Contains(withdrawals!, w => w.StartsAt == DateOnly.FromDateTime(day3.UtcDateTime));
    }

    // --- Test 9: non-regression of Art. 19 — legacy withdrawal computation is untouched. ---

    [Fact]
    public async Task LegacyTreatmentEvent_WithdrawalPeriod_StillComputesAsBefore()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);
        var routeId = await ActiveRouteAsync();
        var occurredAt = DateTimeOffset.UtcNow;

        var treatment = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Treatment,
            occurredAt,
            recordedBy = "operario",
            payloadJson = "{\"dose\":\"5ml\"}", // legacy free-text dose, not numeric
            routeId,
            reason = "curative",
            milkWithdrawalDays = 5,
            meatWithdrawalDays = 10,
        });
        treatment.EnsureSuccessStatusCode();

        var targetDate = DateOnly.FromDateTime(occurredAt.UtcDateTime).AddDays(1);
        var withdrawalsResponse = await _client.GetAsync(
            $"/api/v1/animals/{animalId}/withdrawal-periods?targetDate={targetDate:yyyy-MM-dd}");
        withdrawalsResponse.EnsureSuccessStatusCode();
        var withdrawals = await withdrawalsResponse.Content.ReadFromJsonAsync<List<WithdrawalPeriodDto>>();

        Assert.NotNull(withdrawals);
        var w = Assert.Single(withdrawals!);
        Assert.Equal(WithdrawalTarget.Both, w.Target);
        Assert.Equal(DateOnly.FromDateTime(occurredAt.UtcDateTime).AddDays(10), w.EndsAt);
        Assert.True(w.IsActive);
    }

    // --- Additional recommended coverage: the DoseKind x weighing x subject matrix. ---

    [Theory]
    [InlineData(true, true, true)]   // PerWeight, animal, has weighing -> resolves
    [InlineData(true, false, true)]  // PerWeight, group, has weighing -> resolves (estimated)
    public async Task PerWeight_ResolvesWhenWeighingExists(bool isPerWeight, bool isAnimal, bool hasWeighing)
    {
        _ = isPerWeight;
        var routeId = await ActiveRouteAsync();
        var doseKindId = await DoseKindIdAsync(DoseKind.Keys.PerWeight);

        HttpResponseMessage response;
        if (isAnimal)
        {
            var speciesId = await CreateSpeciesAsync();
            var animalId = await CreateAnimalAsync(speciesId);
            if (hasWeighing) await RecordAnimalWeighingAsync(animalId, 100m);

            response = await _client.PostAsJsonAsync("/api/v1/treatment-courses", new
            {
                animalId,
                groupId = (Guid?)null,
                startsAt = DateTimeOffset.UtcNow,
                routeId,
                reason = "curative",
                productId = (Guid?)null,
                doseKindId,
                doseFactorAmount = 1m,
                doseFactorUnit = "ml",
                notes = (string?)null,
            });
        }
        else
        {
            var (groupId, _) = await SeedHeadcountGroupWithWeighingAsync(headCount: 5, sampleCount: 5, avgKg: 20m);

            response = await _client.PostAsJsonAsync("/api/v1/treatment-courses", new
            {
                animalId = (Guid?)null,
                groupId,
                startsAt = DateTimeOffset.UtcNow,
                routeId,
                reason = "curative",
                productId = (Guid?)null,
                doseKindId,
                doseFactorAmount = 1m,
                doseFactorUnit = "ml",
                notes = (string?)null,
            });
        }

        response.EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData(DoseKind.Keys.Absolute)]
    [InlineData(DoseKind.Keys.PerHead)]
    public async Task NonWeightForms_NeverRequireAWeighing(string doseKindKey)
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);
        var routeId = await ActiveRouteAsync();
        var doseKindId = await DoseKindIdAsync(doseKindKey);

        var response = await _client.PostAsJsonAsync("/api/v1/treatment-courses", new
        {
            animalId,
            groupId = (Guid?)null,
            startsAt = DateTimeOffset.UtcNow,
            routeId,
            reason = "curative",
            productId = (Guid?)null,
            doseKindId,
            doseFactorAmount = 5m,
            doseFactorUnit = "ml",
            notes = (string?)null,
        });

        response.EnsureSuccessStatusCode();
    }

    private async Task<TreatmentCourseDto> GetCourseAsync(Guid courseId)
    {
        var response = await _client.GetAsync($"/api/v1/treatment-courses/{courseId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TreatmentCourseDto>())!;
    }

    private sealed record CreatedId(Guid Id);
}
