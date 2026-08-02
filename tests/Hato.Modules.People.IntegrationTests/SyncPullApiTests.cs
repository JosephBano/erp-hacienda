using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hato.Api.Sync;
using Hato.Modules.People.Application.Auth;
using Xunit;

namespace Hato.Modules.People.IntegrationTests;

public class SyncPullApiTests(PeopleApiFactory factory) : IClassFixture<PeopleApiFactory>
{
    private const string UserEmail = "sync-user@finca.ec";
    private const string UserPassword = "SecurePassword123!";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Pull_InitialSync_ReturnsCollectionsAndValidCursor()
    {
        var authedClient = await CreateAuthenticatedClientAsync();

        var response = await authedClient.GetAsync("/api/v1/sync/pull?batchSize=100");
        response.EnsureSuccessStatusCode();

        var pull = await response.Content.ReadFromJsonAsync<SyncPullResponseDto>();
        Assert.NotNull(pull);
        Assert.NotNull(pull!.Collections);
        Assert.NotNull(pull.Collections.Animals);
        Assert.NotNull(pull.Collections.AnimalGroups);
        Assert.NotNull(pull.Collections.Species);
    }

    [Fact]
    public async Task Pull_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/sync/pull");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        await _client.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Usuario Sincronización",
            email = UserEmail,
            password = UserPassword,
            role = "registrar"
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/people/auth/login", new
        {
            email = UserEmail,
            password = UserPassword
        });
        loginResponse.EnsureSuccessStatusCode();

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResultDto>();
        Assert.NotNull(login);

        var authedClient = factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return authedClient;
    }
}
