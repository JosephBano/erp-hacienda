using System.Net.Http.Json;
using Hato.Modules.Production.Application.Milking;
using Hato.Modules.Production.Domain;
using Xunit;

namespace Hato.Modules.Production.IntegrationTests;

public class MilkingApiTests(ProductionApiFactory factory) : IClassFixture<ProductionApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RecordMilkingSession_WithIndividualYields_RoundTripsThroughPostgres()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var animal1 = Guid.NewGuid();
        var animal2 = Guid.NewGuid();

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

    private sealed record CreatedId(Guid Id);
}
