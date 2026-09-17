using System.Net;
using System.Net.Http.Json;
using Hato.Modules.Production.Application.Milking;
using Hato.Modules.Production.Domain;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Hato.Modules.Production.IntegrationTests;

public class MilkingApiTests(ProductionApiFactory factory) : IClassFixture<ProductionApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RecordMilkingSession_WithIndividualYields_RoundTripsThroughPostgres()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Bovino-{Guid.NewGuid()}",
            gestationDays = 283,
            isMilkable = true
        });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var cow1Response = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Female" });
        cow1Response.EnsureSuccessStatusCode();
        var animal1 = (await cow1Response.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var cow2Response = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Female" });
        cow2Response.EnsureSuccessStatusCode();
        var animal2 = (await cow2Response.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var recordResponse = await _client.PostAsJsonAsync("/api/v1/milking-sessions", new
        {
            date = today,
            shift = MilkingShift.Morning,
            recordedBy = "mayordomo 1",
            totalLiters = 35.0m,
            notes = "Ordeño matutino con clima lluvioso",
            individualYields = new[]
            {
                new { animalId = animal1, liters = 16.5m },
                new { animalId = animal2, liters = 18.5m }
            }
        });
        recordResponse.EnsureSuccessStatusCode();

        var getResponse = await _client.GetAsync($"/api/v1/milking-sessions?date={today:yyyy-MM-dd}");
        getResponse.EnsureSuccessStatusCode();
        var sessions = await getResponse.Content.ReadFromJsonAsync<List<MilkingSessionDto>>();

        Assert.NotNull(sessions);
        Assert.NotEmpty(sessions!);

        var session = sessions.First(s => s.RecordedBy == "mayordomo 1");
        Assert.Equal(35.0m, session.TotalLiters);
        Assert.Equal(2, session.Yields.Count);
    }

    [Fact]
    public async Task RecordMilkingSession_WithMaleAnimal_ReturnsProblemDetailsBadRequest()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Bovino-{Guid.NewGuid()}",
            gestationDays = 283,
            isMilkable = true
        });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var bullResponse = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Male" });
        bullResponse.EnsureSuccessStatusCode();
        var bullId = (await bullResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var recordResponse = await _client.PostAsJsonAsync("/api/v1/milking-sessions", new
        {
            date = today,
            shift = MilkingShift.Morning,
            recordedBy = "mayordomo 1",
            totalLiters = 15.0m,
            individualYields = new[]
            {
                new { animalId = bullId, liters = 15.0m }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, recordResponse.StatusCode);
        var problem = await recordResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Solo se pueden ordeñar animales de sexo hembra", problem.Detail);
    }

    [Fact]
    public async Task RecordMilkingSession_WithDisposedAnimal_ReturnsProblemDetailsBadRequest()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Bovino-{Guid.NewGuid()}",
            gestationDays = 283,
            isMilkable = true
        });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var cowResponse = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Female" });
        cowResponse.EnsureSuccessStatusCode();
        var cowId = (await cowResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var eventResponse = await _client.PostAsJsonAsync($"/api/v1/animals/{cowId}/events", new
        {
            eventType = "Disposal",
            occurredAt = DateTimeOffset.UtcNow.AddDays(-1),
            recordedBy = "admin",
            payloadJson = "{\"reason\":\"Venta\"}"
        });
        eventResponse.EnsureSuccessStatusCode();

        var recordResponse = await _client.PostAsJsonAsync("/api/v1/milking-sessions", new
        {
            date = today,
            shift = MilkingShift.Morning,
            recordedBy = "mayordomo 1",
            totalLiters = 12.0m,
            individualYields = new[]
            {
                new { animalId = cowId, liters = 12.0m }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, recordResponse.StatusCode);
        var problem = await recordResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("El animal fue dado de baja y no puede ser ordeñado", problem.Detail);
    }

    [Fact]
    public async Task RecordMilkingSession_WithNonMilkableSpecies_ReturnsProblemDetailsBadRequest()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Porcino-{Guid.NewGuid()}",
            gestationDays = 114,
            isMilkable = false
        });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var sowResponse = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Female" });
        sowResponse.EnsureSuccessStatusCode();
        var sowId = (await sowResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var recordResponse = await _client.PostAsJsonAsync("/api/v1/milking-sessions", new
        {
            date = today,
            shift = MilkingShift.Morning,
            recordedBy = "mayordomo 1",
            totalLiters = 5.0m,
            individualYields = new[]
            {
                new { animalId = sowId, liters = 5.0m }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, recordResponse.StatusCode);
        var problem = await recordResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("no está habilitada para ordeño", problem.Detail);
    }

    private sealed record CreatedId(Guid Id);
}
