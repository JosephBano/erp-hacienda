using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hato.Modules.People.Application.Auth;
using Xunit;

namespace Hato.Modules.People.IntegrationTests;

public class AdminDeactivationRaceApiTests(PeopleApiFactory factory) : IClassFixture<PeopleApiFactory>
{
    [Fact]
    public async Task ConcurrentMutualDeactivation_NeverLeavesZeroActiveAdmins()
    {
        var bootstrapClient = factory.CreateClient();
        await bootstrapClient.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Admin Uno",
            email = "admin-uno@finca.ec",
            password = "SecurePassword123!",
            role = "admin"
        });
        var adminOneClient = await LoginAsync(bootstrapClient, "admin-uno@finca.ec", "SecurePassword123!");

        var registerTwoResponse = await adminOneClient.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Admin Dos",
            email = "admin-dos@finca.ec",
            password = "SecurePassword123!",
            role = "admin"
        });
        registerTwoResponse.EnsureSuccessStatusCode();
        var adminTwoId = (await registerTwoResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;
        var adminTwoClient = await LoginAsync(factory.CreateClient(), "admin-dos@finca.ec", "SecurePassword123!");

        var usersResponse = await adminOneClient.GetAsync("/api/v1/people/users");
        var users = await usersResponse.Content.ReadFromJsonAsync<List<UserRecord>>();
        var adminOneId = users!.Single(u => u.Email == "admin-uno@finca.ec").Id;

        var deactivateTwo = adminOneClient.PostAsync($"/api/v1/people/users/{adminTwoId}/deactivate", null);
        var deactivateOne = adminTwoClient.PostAsync($"/api/v1/people/users/{adminOneId}/deactivate", null);

        var results = await Task.WhenAll(deactivateTwo, deactivateOne);

        Assert.True(
            results.Count(r => r.StatusCode == HttpStatusCode.NoContent) <= 1,
            "At most one mutual deactivation may succeed — both succeeding leaves zero active Admins.");
    }

    private static async Task<HttpClient> LoginAsync(HttpClient anonymousClient, string email, string password)
    {
        var loginResponse = await anonymousClient.PostAsJsonAsync("/api/v1/people/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResultDto>();
        anonymousClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return anonymousClient;
    }

    private sealed record CreatedId(Guid Id);
    private sealed record UserRecord(Guid Id, string Email);
}
