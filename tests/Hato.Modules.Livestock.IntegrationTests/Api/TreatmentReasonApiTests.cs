using System.Net;
using System.Net.Http.Json;
using Hato.Modules.Livestock.Application.TreatmentCatalogs.TreatmentReasons;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// 3.5a.2-A — treatment reason catalog: scheduled/curative/preventive.
/// </summary>
public class TreatmentReasonApiTests(HatoApiFactory factory) : IClassFixture<HatoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Catalog_IsSeededWithTheThreeFoundationalReasons()
    {
        var response = await _client.GetAsync("/api/v1/treatment-reasons");
        response.EnsureSuccessStatusCode();

        var reasons = await response.Content.ReadFromJsonAsync<List<TreatmentReasonDto>>();

        Assert.NotNull(reasons);
        Assert.Contains(reasons!, r => r.Key == "scheduled");
        Assert.Contains(reasons!, r => r.Key == "curative");
        Assert.Contains(reasons!, r => r.Key == "preventive");
    }

    [Fact]
    public async Task Create_ThenDeactivate_HidesItFromTheDefaultList()
    {
        var key = $"test_reason_{Guid.NewGuid():N}";

        var create = await _client.PostAsJsonAsync(
            "/api/v1/treatment-reasons",
            new { key, labelEs = "Motivo de prueba" });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var deactivate = await _client.DeleteAsync($"/api/v1/treatment-reasons/{id}");
        deactivate.EnsureSuccessStatusCode();

        var list = await _client.GetAsync("/api/v1/treatment-reasons");
        var visible = await list.Content.ReadFromJsonAsync<List<TreatmentReasonDto>>();
        Assert.DoesNotContain(visible!, r => r.Id == id);

        var listAll = await _client.GetAsync("/api/v1/treatment-reasons?includeInactive=true");
        var all = await listAll.Content.ReadFromJsonAsync<List<TreatmentReasonDto>>();
        Assert.Contains(all!, r => r.Id == id && !r.IsActive);
    }

    [Fact]
    public async Task Create_WithDuplicateKey_IsRejected()
    {
        var key = $"dup_{Guid.NewGuid():N}";

        var first = await _client.PostAsJsonAsync(
            "/api/v1/treatment-reasons",
            new { key, labelEs = "Primero" });
        first.EnsureSuccessStatusCode();

        var dup = await _client.PostAsJsonAsync(
            "/api/v1/treatment-reasons",
            new { key, labelEs = "Duplicado" });

        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidKey_IsRejected()
    {
        // Whitespace in the key is rejected (the key is the wire identifier,
        // it has to be lower-snake-case). Use a random key suffix to avoid
        // colliding with the seeded reasons.
        var bad = await _client.PostAsJsonAsync(
            "/api/v1/treatment-reasons",
            new { key = "curative care", labelEs = "Curativa" });

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        // Upper-case letters are accepted, normalised silently. Make sure
        // the API reflects the domain's contract with a fresh key.
        var accepted = await _client.PostAsJsonAsync(
            "/api/v1/treatment-reasons",
            new { key = $"UPPER_TEST_{Guid.NewGuid():N}", labelEs = "Normalizada" });
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task Create_WithEmptyLabel_IsRejected()
    {
        var bad = await _client.PostAsJsonAsync(
            "/api/v1/treatment-reasons",
            new { key = $"valid_key_{Guid.NewGuid():N}", labelEs = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task PatchLabel_UpdatesOnlyTheLabel()
    {
        var key = $"patch_{Guid.NewGuid():N}";
        var create = await _client.PostAsJsonAsync(
            "/api/v1/treatment-reasons",
            new { key, labelEs = "Original" });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var patch = await _client.PatchAsync(
            $"/api/v1/treatment-reasons/{id}/label",
            JsonContent.Create(new { labelEs = "Renombrado" }));
        patch.EnsureSuccessStatusCode();

        var list = await _client.GetAsync("/api/v1/treatment-reasons");
        var reasons = await list.Content.ReadFromJsonAsync<List<TreatmentReasonDto>>();
        var row = Assert.Single(reasons!, r => r.Id == id);
        Assert.Equal("Renombrado", row.LabelEs);
        Assert.Equal(key, row.Key);
    }

    private sealed record CreatedId(Guid Id);
}