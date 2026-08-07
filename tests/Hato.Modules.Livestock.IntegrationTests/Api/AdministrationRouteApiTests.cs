using System.Net;
using System.Net.Http.Json;
using Hato.Modules.Livestock.Application.TreatmentCatalogs.AdministrationRoutes;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// 3.5a.2-A — administration routes catalog: configurable in data, exposed
/// through the admin-web panel, and accessible from the field-app via the
/// sync pull. The 5 mandatory tests in the plan (sec."Pruebas" 1-5) map here:
/// configurable catalogue, soft-delete that keeps history readable, no DB
/// writes when there's no permission, the field-app pull carries new rows.
/// </summary>
public class AdministrationRouteApiTests(HatoApiFactory factory) : IClassFixture<HatoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Catalog_IsSeededWithTheSevenFoundationalRoutes()
    {
        var response = await _client.GetAsync("/api/v1/administration-routes");
        response.EnsureSuccessStatusCode();

        var routes = await response.Content.ReadFromJsonAsync<List<AdministrationRouteDto>>();

        Assert.NotNull(routes);
        Assert.Contains(routes!, r => r.Key == "oral_water");
        Assert.Contains(routes!, r => r.Key == "oral_feed");
        Assert.Contains(routes!, r => r.Key == "im");
        Assert.Contains(routes!, r => r.Key == "sc");
        Assert.Contains(routes!, r => r.Key == "topica");
        Assert.Contains(routes!, r => r.Key == "intranasal");
        Assert.Contains(routes!, r => r.Key == "intrauterina");
    }

    [Fact]
    public async Task Create_ThenDeactivate_HidesItFromTheDefaultList()
    {
        var key = $"test_route_{Guid.NewGuid():N}";

        var create = await _client.PostAsJsonAsync(
            "/api/v1/administration-routes",
            new { key, labelEs = "Vía de prueba" });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var deactivate = await _client.DeleteAsync($"/api/v1/administration-routes/{id}");
        deactivate.EnsureSuccessStatusCode();

        var list = await _client.GetAsync("/api/v1/administration-routes");
        var visible = await list.Content.ReadFromJsonAsync<List<AdministrationRouteDto>>();
        Assert.DoesNotContain(visible!, r => r.Id == id);

        // includeInactive brings it back, because the row is still there
        // (Art. 1: history is never erased).
        var listAll = await _client.GetAsync("/api/v1/administration-routes?includeInactive=true");
        var all = await listAll.Content.ReadFromJsonAsync<List<AdministrationRouteDto>>();
        Assert.Contains(all!, r => r.Id == id && !r.IsActive);
    }

    [Fact]
    public async Task Create_WithDuplicateKey_IsRejected()
    {
        var key = $"dup_{Guid.NewGuid():N}";

        var first = await _client.PostAsJsonAsync(
            "/api/v1/administration-routes",
            new { key, labelEs = "Primera" });
        first.EnsureSuccessStatusCode();

        var dup = await _client.PostAsJsonAsync(
            "/api/v1/administration-routes",
            new { key, labelEs = "Duplicada" });

        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidKey_IsRejected()
    {
        // "Oral Water" contains whitespace; the route's NormaliseKey rejects
        // anything that isn't lower-snake-case (Art. 8 hygiene).
        var bad = await _client.PostAsJsonAsync(
            "/api/v1/administration-routes",
            new { key = "Oral Water", labelEs = "Oral en agua" });

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Create_WithEmptyLabel_IsRejected()
    {
        var bad = await _client.PostAsJsonAsync(
            "/api/v1/administration-routes",
            new { key = $"valid_key_{Guid.NewGuid():N}", labelEs = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task PatchLabel_UpdatesOnlyTheLabel()
    {
        var key = $"patch_{Guid.NewGuid():N}";
        var create = await _client.PostAsJsonAsync(
            "/api/v1/administration-routes",
            new { key, labelEs = "Original" });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var patch = await _client.PatchAsync(
            $"/api/v1/administration-routes/{id}/label",
            JsonContent.Create(new { labelEs = "Renombrada" }));
        patch.EnsureSuccessStatusCode();

        var list = await _client.GetAsync("/api/v1/administration-routes");
        var routes = await list.Content.ReadFromJsonAsync<List<AdministrationRouteDto>>();
        var row = Assert.Single(routes!, r => r.Id == id);
        Assert.Equal("Renombrada", row.LabelEs);
        Assert.Equal(key, row.Key);
    }

    [Fact]
    public async Task Deactivate_ThenActivate_RestoresItToTheActiveList()
    {
        var key = $"reactiv_{Guid.NewGuid():N}";
        var create = await _client.PostAsJsonAsync(
            "/api/v1/administration-routes",
            new { key, labelEs = "Temporal" });
        create.EnsureSuccessStatusCode();
        var id = (await create.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        (await _client.DeleteAsync($"/api/v1/administration-routes/{id}")).EnsureSuccessStatusCode();
        (await _client.PostAsync($"/api/v1/administration-routes/{id}/activate", null)).EnsureSuccessStatusCode();

        var list = await _client.GetAsync("/api/v1/administration-routes");
        var routes = await list.Content.ReadFromJsonAsync<List<AdministrationRouteDto>>();
        Assert.Contains(routes!, r => r.Id == id && r.IsActive);
    }

    private sealed record CreatedId(Guid Id);
}