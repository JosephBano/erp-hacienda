using System.Net.Http.Json;
using Xunit;

namespace Hato.Modules.Tasks.IntegrationTests.Api;

/// <summary>
/// Exercises GenerateAlertsCommand through the real host with Livestock, Breeding and
/// Inventory migrated — this is the coverage that would have caught the raw-SQL bug
/// (wrong column name, DateOnly/DateTimeOffset mismatch) before it shipped.
/// </summary>
public class AlertGenerationApiTests(TasksApiFactory factory) : IClassFixture<TasksApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GenerateAlerts_CreatesOneAlertPerActiveCondition()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Upcoming birth: a service + a positive check with a short gestation window.
        var damWithBirthResponse = await _client.PostAsJsonAsync("/api/v1/breeding/services", new
        {
            damId = Guid.NewGuid(),
            serviceType = "Natural",
            serviceDate = today.AddDays(-30),
            sireAnimalId = Guid.NewGuid()
        });
        damWithBirthResponse.EnsureSuccessStatusCode();
        var serviceWithBirth = await damWithBirthResponse.Content.ReadFromJsonAsync<ServiceDto>();

        var checkResponse = await _client.PostAsJsonAsync("/api/v1/breeding/pregnancy-checks", new
        {
            serviceId = serviceWithBirth!.Id,
            checkDate = today,
            method = "Palpation",
            result = "Positive",
            gestationDays = 5
        });
        checkResponse.EnsureSuccessStatusCode();

        // 2. Pending pregnancy check: a service old enough with no check recorded.
        var pendingServiceResponse = await _client.PostAsJsonAsync("/api/v1/breeding/services", new
        {
            damId = Guid.NewGuid(),
            serviceType = "Natural",
            serviceDate = today.AddDays(-40),
            sireAnimalId = Guid.NewGuid()
        });
        pendingServiceResponse.EnsureSuccessStatusCode();

        // 3. Active withdrawal: register an animal, then a treatment with a milk withdrawal.
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = "Bovino", gestationDays = 283 });
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

        // 4. Expiring inventory batch.
        var itemResponse = await _client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = "Vacuna Aftosa",
            category = "Medicine",
            unit = "dosis",
            minStock = 10m
        });
        itemResponse.EnsureSuccessStatusCode();
        var itemId = (await itemResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var batchResponse = await _client.PostAsJsonAsync($"/api/v1/inventory/items/{itemId}/batches", new
        {
            batchNumber = "LOT-EXP-1",
            quantity = 50m,
            costPerUnit = 1.2m,
            expirationDate = today.AddDays(10)
        });
        batchResponse.EnsureSuccessStatusCode();

        // Act
        var generateResponse = await _client.PostAsync("/api/v1/alerts/generate", null);
        generateResponse.EnsureSuccessStatusCode();
        var generated = await generateResponse.Content.ReadFromJsonAsync<GeneratedAlertsResult>();

        Assert.NotNull(generated);
        Assert.True(generated!.GeneratedAlerts >= 4);

        var alertsResponse = await _client.GetAsync("/api/v1/alerts");
        alertsResponse.EnsureSuccessStatusCode();
        var alerts = await alertsResponse.Content.ReadFromJsonAsync<List<AlertDto>>();

        Assert.NotNull(alerts);
        Assert.Contains(alerts!, a => a.Code == "UPCOMING_BIRTH");
        Assert.Contains(alerts!, a => a.Code == "WITHDRAWAL_PERIOD_ACTIVE");
        Assert.Contains(alerts!, a => a.Code == "PENDING_PREGNANCY_CHECK");
        Assert.Contains(alerts!, a => a.Code == "INVENTORY_BATCH_EXPIRING");

        // Idempotency: generating again must not duplicate the same alerts.
        var secondGenerateResponse = await _client.PostAsync("/api/v1/alerts/generate", null);
        secondGenerateResponse.EnsureSuccessStatusCode();
        var secondGenerated = await secondGenerateResponse.Content.ReadFromJsonAsync<GeneratedAlertsResult>();
        Assert.Equal(0, secondGenerated!.GeneratedAlerts);
    }

    private sealed record CreatedId(Guid Id);
    private sealed record ServiceDto(Guid Id);
    private sealed record GeneratedAlertsResult(int GeneratedAlerts);
    private sealed record AlertDto(Guid Id, string Code, string Title, string Message, string Severity, Guid? TargetEntityId, bool IsDismissed, DateTimeOffset CreatedAt);
}
