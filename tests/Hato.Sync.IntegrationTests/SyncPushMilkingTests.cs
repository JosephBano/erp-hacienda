using System.Net.Http.Json;
using System.Text.Json;
using Hato.Modules.Production.Domain;
using Xunit;

namespace Hato.Sync.IntegrationTests;

[Collection(SyncCollection.Name)]
public class SyncPushMilkingTests(SyncApiFactory factory)
{
    [Fact]
    public async Task Push_IndividualMilking_WithPlausibilityConfirmedFalse_Succeeds()
    {
        var context = await SyncTestContext.CreateAsync(factory, "milking-push-false");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var cowId = await context.CreateAnimalAsync(speciesId, "Female");

        var payload = new
        {
            date = DateOnly.FromDateTime(DateTime.UtcNow),
            shift = "Morning",
            recordedBy = "tester@hato",
            totalLiters = 14.5m,
            individualYields = new[]
            {
                new { animalId = cowId, liters = 14.5m }
            },
            isPlausibilityConfirmed = false
        };

        var result = await context.PushAsync("recordMilking", payload);

        Assert.Equal("Accepted", result.GetProperty("status").GetString());
        var sessionId = Guid.Parse(result.GetProperty("resultRef").GetString()!);
        Assert.NotEqual(Guid.Empty, sessionId);
    }

    [Fact]
    public async Task Push_IndividualMilking_WithPlausibilityConfirmedTrue_SucceedsAndPersistsFlag()
    {
        var context = await SyncTestContext.CreateAsync(factory, "milking-push-true");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var cowId = await context.CreateAnimalAsync(speciesId, "Female");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var payload = new
        {
            date = today,
            shift = "Afternoon",
            recordedBy = "tester@hato",
            totalLiters = 28.0m,
            individualYields = new[]
            {
                new { animalId = cowId, liters = 28.0m }
            },
            isPlausibilityConfirmed = true
        };

        var result = await context.PushAsync("recordMilking", payload);

        Assert.Equal("Accepted", result.GetProperty("status").GetString());
        var sessionId = Guid.Parse(result.GetProperty("resultRef").GetString()!);
        Assert.NotEqual(Guid.Empty, sessionId);

        var sessionsResponse = await context.Client.GetAsync($"/api/v1/milking-sessions?date={today:O}");
        sessionsResponse.EnsureSuccessStatusCode();
        var sessions = await sessionsResponse.Content.ReadFromJsonAsync<JsonElement>();

        var sessionElement = sessions.EnumerateArray().FirstOrDefault(s => s.GetProperty("id").GetGuid() == sessionId);
        Assert.True(sessionElement.ValueKind != JsonValueKind.Undefined, "Session not found in GET response");
        Assert.True(sessionElement.GetProperty("isPlausibilityConfirmed").GetBoolean());
    }

    [Fact]
    public async Task Push_IndividualMilking_WithMaleAnimal_ReturnsRejected()
    {
        var context = await SyncTestContext.CreateAsync(factory, "milking-push-male");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var bullId = await context.CreateAnimalAsync(speciesId, "Male");

        var payload = new
        {
            date = DateOnly.FromDateTime(DateTime.UtcNow),
            shift = "Morning",
            recordedBy = "tester@hato",
            totalLiters = 14.5m,
            individualYields = new[]
            {
                new { animalId = bullId, liters = 14.5m }
            },
            isPlausibilityConfirmed = false
        };

        var result = await context.PushAsync("recordMilking", payload);

        Assert.Equal("Rejected", result.GetProperty("status").GetString());
        var error = result.GetProperty("errorDetails").GetString();
        Assert.NotNull(error);
        Assert.Contains("Solo se pueden ordeñar animales de sexo hembra", error);
    }

    [Fact]
    public async Task Push_IndividualMilking_WithNonMilkableSpecies_ReturnsRejected()
    {
        var context = await SyncTestContext.CreateAsync(factory, "milking-push-non-milkable");
        var speciesId = await context.CreateSpeciesAsync("Porcino", isMilkable: false);
        var sowId = await context.CreateAnimalAsync(speciesId, "Female");

        var payload = new
        {
            date = DateOnly.FromDateTime(DateTime.UtcNow),
            shift = "Morning",
            recordedBy = "tester@hato",
            totalLiters = 5.0m,
            individualYields = new[]
            {
                new { animalId = sowId, liters = 5.0m }
            },
            isPlausibilityConfirmed = false
        };

        var result = await context.PushAsync("recordMilking", payload);

        Assert.Equal("Rejected", result.GetProperty("status").GetString());
        var error = result.GetProperty("errorDetails").GetString();
        Assert.NotNull(error);
        Assert.Contains("no está habilitada para ordeño", error);
    }
}
