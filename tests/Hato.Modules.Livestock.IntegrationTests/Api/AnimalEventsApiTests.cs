using System.Net.Http.Json;
using Hato.Modules.Livestock.Application.Events;
using Hato.Modules.Livestock.Domain;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

public class AnimalEventsApiTests(HatoApiFactory factory) : IClassFixture<HatoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RecordTreatmentEvent_CreatesWithdrawalPeriod_AndFetchesHistory()
    {
        // 1. Setup Animal
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = $"Bovino-{Guid.NewGuid():N}", gestationDays = 283 });
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var animalResponse = await _client.PostAsJsonAsync("/api/v1/animals", new
        {
            speciesId,
            sex = Sex.Female,
            birthDate = (DateOnly?)null,
            breedId = (Guid?)null,
            categoryId = (Guid?)null,
        });
        var animalId = (await animalResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        // 2. Record Treatment Event with 7 days milk withdrawal
        var treatmentPayload = "{\"medicine\": \"Oxitetraciclina\", \"dose\": \"10ml\", \"veterinarian\": \"Dr. Perez\"}";
        var recordResponse = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Treatment,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "mayordomo",
            payloadJson = treatmentPayload,
            cost = 25.50m,
            milkWithdrawalDays = 7
        });
        recordResponse.EnsureSuccessStatusCode();

        // 3. Get Events History
        var eventsResponse = await _client.GetAsync($"/api/v1/animals/{animalId}/events");
        eventsResponse.EnsureSuccessStatusCode();
        var events = await eventsResponse.Content.ReadFromJsonAsync<List<AnimalEventDto>>();

        Assert.NotNull(events);
        Assert.Single(events!);
        Assert.Equal(EventType.Treatment, events[0].EventType);
        Assert.Equal(25.50m, events[0].Cost);

        // 4. Check Active Withdrawal Period
        var withdrawalResponse = await _client.GetAsync($"/api/v1/animals/{animalId}/withdrawal-periods");
        withdrawalResponse.EnsureSuccessStatusCode();
        var withdrawals = await withdrawalResponse.Content.ReadFromJsonAsync<List<WithdrawalPeriodDto>>();

        Assert.NotNull(withdrawals);
        Assert.Single(withdrawals!);
        Assert.Equal(WithdrawalTarget.Milk, withdrawals[0].Target);
        Assert.True(withdrawals[0].IsActive);
    }

    [Fact]
    public async Task RecordTreatmentEvent_WithNonUtcOffset_UsesUtcDateForWithdrawalStart()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = $"Bovino-UTC-{Guid.NewGuid()}", gestationDays = 283 });
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var animalResponse = await _client.PostAsJsonAsync("/api/v1/animals", new
        {
            speciesId,
            sex = Sex.Female,
            birthDate = (DateOnly?)null,
            breedId = (Guid?)null,
            categoryId = (Guid?)null,
        });
        var animalId = (await animalResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        // 23:00 in Ecuador (UTC-05:00) on 2026-08-01 is already 2026-08-02 in UTC.
        // The withdrawal period must start on the UTC date, not the local wall-clock date.
        var occurredAt = new DateTimeOffset(2026, 8, 1, 23, 0, 0, TimeSpan.FromHours(-5));
        var expectedUtcDate = DateOnly.FromDateTime(occurredAt.UtcDateTime);

        var recordResponse = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Treatment,
            occurredAt,
            recordedBy = "mayordomo",
            payloadJson = "{\"medicine\": \"Oxitetraciclina\"}",
            milkWithdrawalDays = 7
        });
        recordResponse.EnsureSuccessStatusCode();

        // The endpoint defaults `targetDate` to DateTime.UtcNow and filters the
        // response to withdrawals whose window covers it. The hardcoded `occurredAt`
        // above is in 2026-08; once the wall clock has passed the 7-day window the
        // withdrawal is no longer "active" and a date-less GET would return [].
        // Pin `targetDate` to the day we just anchored the window on — same
        // convention as TreatmentCourseApiTests (line 448 et seq.).
        var withdrawalResponse = await _client.GetAsync(
            $"/api/v1/animals/{animalId}/withdrawal-periods?targetDate={expectedUtcDate:yyyy-MM-dd}");
        withdrawalResponse.EnsureSuccessStatusCode();
        var withdrawals = await withdrawalResponse.Content.ReadFromJsonAsync<List<WithdrawalPeriodDto>>();

        Assert.NotNull(withdrawals);
        Assert.Single(withdrawals!);
        Assert.Equal(expectedUtcDate, withdrawals[0].StartsAt);
    }

    [Fact]
    public async Task RecordEvent_OnDisposedAnimal_AfterDisposalDate_ReturnsProblemDetails()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = $"Bovino-{Guid.NewGuid():N}", gestationDays = 283 });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var animalResponse = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = Sex.Female });
        animalResponse.EnsureSuccessStatusCode();
        var animalId = (await animalResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var disposalTime = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);
        var disposeResponse = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Disposal,
            occurredAt = disposalTime,
            recordedBy = "mayordomo",
            payloadJson = "{}"
        });
        disposeResponse.EnsureSuccessStatusCode();

        // Event on or after disposal
        var eventResponse = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Weighing,
            occurredAt = disposalTime.AddHours(1),
            recordedBy = "mayordomo",
            payloadJson = "{\"weightKg\": 400}"
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, eventResponse.StatusCode);
        var body = await eventResponse.Content.ReadAsStringAsync();
        Assert.Contains("baja", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecordDisposal_OnAlreadyDisposedAnimal_ReturnsProblemDetails()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = $"Bovino-{Guid.NewGuid():N}", gestationDays = 283 });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var animalResponse = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = Sex.Male });
        animalResponse.EnsureSuccessStatusCode();
        var animalId = (await animalResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var disposalTime = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);
        var firstDispose = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Disposal,
            occurredAt = disposalTime,
            recordedBy = "mayordomo",
            payloadJson = "{}"
        });
        firstDispose.EnsureSuccessStatusCode();

        // Second disposal
        var secondDispose = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Disposal,
            occurredAt = disposalTime.AddDays(1),
            recordedBy = "mayordomo",
            payloadJson = "{}"
        });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, secondDispose.StatusCode);
        var body = await secondDispose.Content.ReadAsStringAsync();
        Assert.Contains("baja", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecordEvent_OnDisposedAnimal_BeforeDisposalDate_Succeeds()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = $"Bovino-{Guid.NewGuid():N}", gestationDays = 283 });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var animalResponse = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = Sex.Female });
        animalResponse.EnsureSuccessStatusCode();
        var animalId = (await animalResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var disposalTime = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);
        var disposeResponse = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Disposal,
            occurredAt = disposalTime,
            recordedBy = "mayordomo",
            payloadJson = "{}"
        });
        disposeResponse.EnsureSuccessStatusCode();

        // Retrospective weighing before disposal is valid per D5
        var eventResponse = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = EventType.Weighing,
            occurredAt = disposalTime.AddDays(-5),
            recordedBy = "mayordomo",
            payloadJson = "{\"weightKg\": 390}"
        });

        eventResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task WeighingAndTreatment_BothMaleAndFemale_Succeeds()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = $"Bovino-{Guid.NewGuid():N}", gestationDays = 283 });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var maleResponse = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = Sex.Male });
        maleResponse.EnsureSuccessStatusCode();
        var maleId = (await maleResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var femaleResponse = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = Sex.Female });
        femaleResponse.EnsureSuccessStatusCode();
        var femaleId = (await femaleResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        // Weigh male
        var weighMale = await _client.PostAsJsonAsync($"/api/v1/animals/{maleId}/events", new
        {
            eventType = EventType.Weighing,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "operador",
            payloadJson = "{\"weightKg\": 720}"
        });
        weighMale.EnsureSuccessStatusCode();

        // Weigh female
        var weighFemale = await _client.PostAsJsonAsync($"/api/v1/animals/{femaleId}/events", new
        {
            eventType = EventType.Weighing,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "operador",
            payloadJson = "{\"weightKg\": 510}"
        });
        weighFemale.EnsureSuccessStatusCode();

        // Treat male
        var treatMale = await _client.PostAsJsonAsync($"/api/v1/animals/{maleId}/events", new
        {
            eventType = EventType.Treatment,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "veterinario",
            payloadJson = "{\"medicine\": \"Vitamina\"}"
        });
        treatMale.EnsureSuccessStatusCode();
    }

    private sealed record CreatedId(Guid Id);
}
