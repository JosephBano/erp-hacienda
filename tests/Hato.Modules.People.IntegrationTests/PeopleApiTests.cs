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
    private const string AdminEmail = "admin@finca.ec";
    private const string AdminPassword = "SecurePassword123!";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RegisterUser_AndGetUsers_RoundTripsThroughPostgres()
    {
        var authedClient = await CreateAuthenticatedAdminClientAsync();

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
        var authedClient = await CreateAuthenticatedAdminClientAsync();

        var email = "vet@finca.ec";
        await authedClient.PostAsJsonAsync("/api/v1/people/users", new
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

    [Fact]
    public async Task RegisterUser_AsNonAdminAfterBootstrap_IsForbidden()
    {
        var authedClient = await CreateAuthenticatedAdminClientAsync();

        var vetEmail = "vet-no-escalation@finca.ec";
        await authedClient.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Veterinario Sin Privilegios",
            email = vetEmail,
            password = "CorrectPassword123!",
            role = UserRole.Veterinarian
        });

        // A second, unauthenticated caller must not be able to self-register once the
        // farm already has at least one account — the bootstrap window is exactly one
        // account wide, not "until someone happens to pick Admin".
        var anonymousAttempt = await _client.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Intruso",
            email = "intruso@finca.ec",
            password = "WhateverPassword123!",
            role = UserRole.Admin
        });

        Assert.Equal(HttpStatusCode.Forbidden, anonymousAttempt.StatusCode);
    }

    [Fact]
    public async Task DeactivateUser_PreventsFurtherLogin()
    {
        var authedClient = await CreateAuthenticatedAdminClientAsync();

        var email = "empleado-saliente@finca.ec";
        var password = "CorrectPassword123!";
        var registerResponse = await authedClient.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Empleado Saliente",
            email,
            password,
            role = UserRole.Registrar
        });
        registerResponse.EnsureSuccessStatusCode();
        var userId = (await registerResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var deactivateResponse = await authedClient.PostAsync($"/api/v1/people/users/{userId}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/people/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    /// <summary>
    /// Idempotent across the whole test class (shared Postgres via IClassFixture):
    /// the first caller to run this creates the bootstrap Admin, everyone after just
    /// logs in with the same fixed credentials.
    /// </summary>
    private async Task<HttpClient> CreateAuthenticatedAdminClientAsync()
    {
        await _client.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Administrador Finca",
            email = AdminEmail,
            password = AdminPassword,
            role = UserRole.Admin
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

    private sealed record CreatedId(Guid Id);
}
