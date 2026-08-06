using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Hato.Modules.Breeding.IntegrationTests.Api;

/// <summary>
/// Gestation length is species-specific DB configuration (Art. 8: bovine ~283 days,
/// porcine ~114 days), never a hardcoded constant. RecordPregnancyCheckCommand must
/// resolve it from the dam's Species.GestationDays, not default to 283 for everyone.
/// </summary>
public class PregnancyCheckApiTests(BreedingApiFactory factory) : IClassFixture<BreedingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RecordPositiveCheck_ForPorcineDam_UsesSpeciesGestationDays_NotBovineDefault()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = "Porcino", gestationDays = 114 });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var damResponse = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Female" });
        damResponse.EnsureSuccessStatusCode();
        var damId = (await damResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var serviceDate = new DateOnly(2026, 1, 1);
        var serviceResponse = await _client.PostAsJsonAsync("/api/v1/breeding/services", new
        {
            damId,
            serviceType = "Natural",
            serviceDate,
            sireAnimalId = Guid.NewGuid(),
        });
        Assert.True(serviceResponse.IsSuccessStatusCode, await serviceResponse.Content.ReadAsStringAsync());
        var serviceId = (await serviceResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var checkResponse = await _client.PostAsJsonAsync("/api/v1/breeding/pregnancy-checks", new
        {
            serviceId,
            checkDate = new DateOnly(2026, 2, 1),
            method = "Palpation",
            result = "Positive"
        });
        Assert.True(checkResponse.IsSuccessStatusCode, await checkResponse.Content.ReadAsStringAsync());

        var pregnanciesResponse = await _client.GetAsync("/api/v1/breeding/pregnancies/active");
        pregnanciesResponse.EnsureSuccessStatusCode();
        var pregnancies = await pregnanciesResponse.Content.ReadFromJsonAsync<List<PregnancyDto>>();

        var pregnancy = Assert.Single(pregnancies!, p => p.DamId == damId);
        Assert.Equal(serviceDate.AddDays(114), pregnancy.ExpectedBirthDate);
    }

    [Fact]
    public async Task RecordPositiveCheck_ForSpeciesWithoutGestationDays_IsRejected()
    {
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = "SinConfigurar" });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var damResponse = await _client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Female" });
        damResponse.EnsureSuccessStatusCode();
        var damId = (await damResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var serviceResponse = await _client.PostAsJsonAsync("/api/v1/breeding/services", new
        {
            damId,
            serviceType = "Natural",
            serviceDate = new DateOnly(2026, 1, 1),
            sireAnimalId = Guid.NewGuid(),
        });
        serviceResponse.EnsureSuccessStatusCode();
        var serviceId = (await serviceResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var checkResponse = await _client.PostAsJsonAsync("/api/v1/breeding/pregnancy-checks", new
        {
            serviceId,
            checkDate = new DateOnly(2026, 2, 1),
            method = "Palpation",
            result = "Positive"
        });

        Assert.Equal(HttpStatusCode.BadRequest, checkResponse.StatusCode);
    }

    private sealed record CreatedId(Guid Id);
    private sealed record PregnancyDto(Guid Id, Guid DamId, DateOnly ExpectedBirthDate);
}
