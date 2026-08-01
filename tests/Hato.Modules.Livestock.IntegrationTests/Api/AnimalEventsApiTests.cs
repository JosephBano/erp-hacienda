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
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = "Bovino", gestationDays = 283 });
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

    private sealed record CreatedId(Guid Id);
}
