using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hato.Modules.People.Application.Auth;
using Xunit;

namespace Hato.Modules.People.IntegrationTests;

/// <summary>
/// Regression for <c>feature/admin-web-inventory-detail</c> (commit <c>bbe77ca</c>),
/// which silently replaced the <c>AddPolicy("SettingsFarmModulesManage", ...)</c> line
/// in <c>PeopleModule.cs</c> with <c>AddPolicy("InventoryFeedStagesManage", ...)</c>.
/// Without the manage policy, <c>PATCH /api/v1/farm-modules/{key}</c> responded 500 with
/// <c>"The AuthorizationPolicy named: 'SettingsFarmModulesManage' was not found."</c> — and
/// there was no integration test covering the endpoint, so the build stayed green.
///
/// These tests live next to the People integration suite because the endpoint requires
/// the People schema (seeded roles + permissions) to be fully in place.
/// </summary>
public class FarmModulesEndpointsApiTests(PeopleApiFactory factory) : IClassFixture<PeopleApiFactory>
{
    private const string AdminEmail = "admin@finca.ec";
    private const string AdminPassword = "SecurePassword123!";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PatchFarmModule_AsAdmin_Returns200Not500()
    {
        var authedClient = await CreateAuthenticatedAdminClientAsync();

        // Disable a seeded module. The exact key doesn't matter: the regression
        // manifested before the request body was even read (AuthorizationMiddleware
        // resolved the policy name and threw), so any key triggers the bug.
        var response = await authedClient.PatchAsJsonAsync(
            "/api/v1/farm-modules/inventory",
            new { enabled = false, disabledReason = "audit regression test" });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK from PATCH /api/v1/farm-modules/inventory; " +
            $"got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");

        // Restore so the test is idempotent across reruns against the same DB.
        var restore = await authedClient.PatchAsJsonAsync(
            "/api/v1/farm-modules/inventory",
            new { enabled = true, disabledReason = (string?)null });
        restore.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetFarmModules_AsAdmin_ReturnsSeededList()
    {
        var authedClient = await CreateAuthenticatedAdminClientAsync();

        var response = await authedClient.GetAsync("/api/v1/farm-modules");

        response.EnsureSuccessStatusCode();
        var modules = await response.Content.ReadFromJsonAsync<List<FarmModuleDto>>();
        Assert.NotNull(modules);
        Assert.NotEmpty(modules!);
        // The migration that ships with the People module seeds six modules;
        // we only assert the keys we expect, not the entire set, so adding new
        // modules later doesn't break this test.
        var keys = modules!.Select(m => m.Key).ToHashSet();
        Assert.Contains("production", keys);
        Assert.Contains("livestock", keys);
        Assert.Contains("inventory", keys);
        Assert.Contains("breeding", keys);
        Assert.Contains("tasks", keys);
        Assert.Contains("people", keys);
    }

    private async Task<HttpClient> CreateAuthenticatedAdminClientAsync()
    {
        await _client.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Administrador Finca",
            email = AdminEmail,
            password = AdminPassword,
            role = "admin"
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/people/auth/login", new
        {
            email = AdminEmail,
            password = AdminPassword
        });
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResultDto>();
        Assert.NotNull(login);

        var authedClient = factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return authedClient;
    }

    private sealed record FarmModuleDto(Guid Id, string Key, bool Enabled, string? DisabledReason);
}
