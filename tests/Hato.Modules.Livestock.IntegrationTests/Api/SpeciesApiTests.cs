using System.Net;
using System.Net.Http.Json;
using Hato.Modules.Livestock.Contracts;
using Xunit;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// Bug B3 from the Fase 3.5 retrospective: Species carried DaysOfLactation and
/// CohortWindowDays but the only entry point was a method that had no caller, leaving
/// RecordCohortWeaningCommand to refuse with "Configure el parámetro en el panel" — a
/// panel that did not exist for those fields. These tests pin the new PATCH/GET pair that
/// closes the loop end-to-end.
/// </summary>
public class SpeciesApiTests(HatoApiFactory factory) : IClassFixture<HatoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> CreateSpeciesAsync(string? name = null)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = name ?? $"Especie-{Guid.NewGuid():N}",
            gestationDays = 114,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    [Fact]
    public async Task CreateSpecies_WithLactationParameters_PersistsThem()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Porcino-Test-{Guid.NewGuid():N}",
            gestationDays = 114,
            daysOfLactation = 24,
            cohortWindowDays = 5,
        });
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var profile = await _client.GetFromJsonAsync<SpeciesLactationProfile>(
            $"/api/v1/species/{id}/lactation");

        Assert.NotNull(profile);
        Assert.Equal(24, profile!.DaysOfLactation);
        Assert.Equal(5, profile.CohortWindowDays);
    }

    [Fact]
    public async Task PatchLactation_UpdatesValues_AndGetReturnsThem()
    {
        var speciesId = await CreateSpeciesAsync();

        var patchResponse = await _client.PatchAsJsonAsync(
            $"/api/v1/species/{speciesId}/lactation",
            new { daysOfLactation = 28, cohortWindowDays = 4 });
        Assert.Equal(HttpStatusCode.NoContent, patchResponse.StatusCode);

        var profile = await _client.GetFromJsonAsync<SpeciesLactationProfile>(
            $"/api/v1/species/{speciesId}/lactation");

        Assert.NotNull(profile);
        Assert.Equal(28, profile!.DaysOfLactation);
        Assert.Equal(4, profile.CohortWindowDays);
    }

    [Fact]
    public async Task PatchLactation_NegativeDays_IsRejected()
    {
        var speciesId = await CreateSpeciesAsync();

        var patchResponse = await _client.PatchAsJsonAsync(
            $"/api/v1/species/{speciesId}/lactation",
            new { daysOfLactation = -1, cohortWindowDays = 4 });

        Assert.Equal(HttpStatusCode.BadRequest, patchResponse.StatusCode);
    }

    [Fact]
    public async Task GetLactation_OnUnknownSpecies_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/species/{Guid.NewGuid()}/lactation");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record CreatedId(Guid Id);
}