using System.Net;
using System.Net.Http.Json;
using Hato.Modules.People.Application.Auth;
using Xunit;

namespace Hato.Modules.People.IntegrationTests;

public class RefreshTokenApiTests(PeopleApiFactory factory) : IClassFixture<PeopleApiFactory>
{
    private const string UserEmail = "mobile-user@finca.ec";
    private const string UserPassword = "SecurePassword123!";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_ReturnsRefreshTokenInResponse()
    {
        await RegisterUserAsync();

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/people/auth/login", new
        {
            email = UserEmail,
            password = UserPassword
        });
        loginResponse.EnsureSuccessStatusCode();

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResultDto>();
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login!.Token));
        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));
    }

    [Fact]
    public async Task Refresh_WithValidRefreshToken_RotatesTokenAndReturnsNewJwt()
    {
        await RegisterUserAsync();

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/people/auth/login", new
        {
            email = UserEmail,
            password = UserPassword
        });
        loginResponse.EnsureSuccessStatusCode();
        var initialLogin = (await loginResponse.Content.ReadFromJsonAsync<LoginResultDto>())!;

        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/people/auth/refresh", new
        {
            refreshToken = initialLogin.RefreshToken
        });
        refreshResponse.EnsureSuccessStatusCode();

        var newLogin = (await refreshResponse.Content.ReadFromJsonAsync<LoginResultDto>())!;
        Assert.NotNull(newLogin);
        Assert.False(string.IsNullOrWhiteSpace(newLogin.Token));
        Assert.NotEqual(initialLogin.RefreshToken, newLogin.RefreshToken);

        // Attempting to use old refresh token must fail because it was rotated
        var reuseAttempt = await _client.PostAsJsonAsync("/api/v1/people/auth/refresh", new
        {
            refreshToken = initialLogin.RefreshToken
        });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseAttempt.StatusCode);
    }

    [Fact]
    public async Task Revoke_ValidRefreshToken_PreventsSubsequentRefresh()
    {
        await RegisterUserAsync();

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/people/auth/login", new
        {
            email = UserEmail,
            password = UserPassword
        });
        loginResponse.EnsureSuccessStatusCode();
        var login = (await loginResponse.Content.ReadFromJsonAsync<LoginResultDto>())!;

        var revokeResponse = await _client.PostAsJsonAsync("/api/v1/people/auth/revoke", new
        {
            refreshToken = login.RefreshToken
        });
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var refreshAttempt = await _client.PostAsJsonAsync("/api/v1/people/auth/refresh", new
        {
            refreshToken = login.RefreshToken
        });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAttempt.StatusCode);
    }

    private async Task RegisterUserAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Usuario Móvil",
            email = UserEmail,
            password = UserPassword,
            role = "registrar"
        });

        if (response.StatusCode == HttpStatusCode.Created)
        {
            return;
        }
    }
}
