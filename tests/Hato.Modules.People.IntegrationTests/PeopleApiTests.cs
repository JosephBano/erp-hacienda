using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hato.Modules.People.Application.Auth;
using Hato.Modules.People.Application.Users;
using Hato.Modules.People.Domain;
using Xunit;

namespace Hato.Modules.People.IntegrationTests;

public class PeopleApiTests(PeopleApiFactory factory) : IClassFixture<PeopleApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RegisterUser_AndGetUsers_RoundTripsThroughPostgres()
    {
        // The very first user bootstraps as Admin with no token required.
        var adminEmail = "admin@finca.ec";
        var adminPassword = "SecurePassword123!";
        var registerAdminResponse = await _client.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Administrador Finca",
            email = adminEmail,
            password = adminPassword,
            role = UserRole.Admin
        });
        registerAdminResponse.EnsureSuccessStatusCode();

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/people/auth/login", new
        {
            email = adminEmail,
            password = adminPassword
        });
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResultDto>();
        Assert.NotNull(login);
        Assert.Equal(UserRole.Admin, login!.Role);

        using var authedClient = factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        var registerResponse = await authedClient.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Mayordomo Carlos",
            email = "carlos@finca.ec",
            password = "AnotherSecurePassword123!",
            role = UserRole.Registrar
        });
        registerResponse.EnsureSuccessStatusCode();
        var userId = (await registerResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var getUsersResponse = await authedClient.GetAsync("/api/v1/people/users");
        getUsersResponse.EnsureSuccessStatusCode();
        var users = await getUsersResponse.Content.ReadFromJsonAsync<List<UserDto>>();

        Assert.NotNull(users);
        Assert.NotEmpty(users!);
        var user = users!.First(u => u.Id == userId);
        Assert.Equal("Mayordomo Carlos", user.FullName);
        Assert.Equal("carlos@finca.ec", user.Email);
        Assert.Equal(UserRole.Registrar, user.Role);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var email = "vet@finca.ec";
        await _client.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Veterinario Ana",
            email,
            password = "CorrectPassword123!",
            role = UserRole.Veterinarian
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/people/auth/login", new
        {
            email,
            password = "WrongPassword"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task GetUsers_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/people/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record CreatedId(Guid Id);
}
