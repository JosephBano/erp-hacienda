using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Hato.Modules.Production.IntegrationTests;

/// <summary>
/// Art. 19: milk from an animal under an active withdrawal period cannot be sold. This
/// must be enforced at the point where the yield is recorded, not left to a report.
/// </summary>
public class WithdrawalBlockingApiTests(ProductionApiFactory factory) : IClassFixture<ProductionApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RecordMilkingSession_ForAnimalUnderActiveMilkWithdrawal_IsRejected()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = $"Bovino-{Guid.NewGuid()}", gestationDays = 283, isMilkable = true });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var animalResponse = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Female" });
        animalResponse.EnsureSuccessStatusCode();
        var animalId = (await animalResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var eventResponse = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = "Treatment",
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "veterinario 1",
            payloadJson = "{\"medication\":\"Oxitetraciclina\"}",
            milkWithdrawalDays = 7
        });
        eventResponse.EnsureSuccessStatusCode();

        var milkingResponse = await _client.PostAsJsonAsync("/api/v1/milking-sessions", new
        {
            date = today,
            shift = "Morning",
            recordedBy = "mayordomo 1",
            totalLiters = 10.0m,
            individualYields = new[] { new { animalId, liters = 10.0m } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, milkingResponse.StatusCode);
    }

    [Fact]
    public async Task RecordMilkingSession_ForGroupWithAnimalUnderActiveMilkWithdrawal_IsRejected()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = $"Bovino-{Guid.NewGuid()}", gestationDays = 283, isMilkable = true });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var animalResponse = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Female" });
        animalResponse.EnsureSuccessStatusCode();
        var animalId = (await animalResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var eventResponse = await _client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = "Treatment",
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "veterinario 1",
            payloadJson = "{\"medication\":\"Oxitetraciclina\"}",
            milkWithdrawalDays = 7
        });
        eventResponse.EnsureSuccessStatusCode();

        var groupResponse = await _client.PostAsJsonAsync("/api/v1/animal-groups", new { name = $"Ordeño matutino {Guid.NewGuid()}" });
        groupResponse.EnsureSuccessStatusCode();
        var groupId = (await groupResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var memberResponse = await _client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/members", new { animalId, joinedAt = today });
        memberResponse.EnsureSuccessStatusCode();

        // Group/tank total only — no individualYields, the case the original bug skipped.
        var milkingResponse = await _client.PostAsJsonAsync("/api/v1/milking-sessions", new
        {
            date = today,
            shift = "Morning",
            recordedBy = "mayordomo 1",
            groupId,
            totalLiters = 120.0m
        });

        Assert.Equal(HttpStatusCode.BadRequest, milkingResponse.StatusCode);
    }

    private sealed record CreatedId(Guid Id);
}
