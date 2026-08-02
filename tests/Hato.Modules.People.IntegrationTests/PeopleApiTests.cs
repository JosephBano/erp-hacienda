using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hato.Modules.People.Application.Auth;
using Hato.Modules.People.Application.Roles;
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
            role = "registrar"
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
        Assert.Contains("registrar", user.Roles);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task GetRolesAndPermissions_ReturnsSeededData()
    {
        var authedClient = await CreateAuthenticatedAdminClientAsync();

        var permissionsResponse = await authedClient.GetAsync("/api/v1/people/permissions");
        permissionsResponse.EnsureSuccessStatusCode();
        var permissions = await permissionsResponse.Content.ReadFromJsonAsync<List<PermissionDto>>();

        Assert.NotNull(permissions);
        Assert.Contains(permissions!, p => p.Code == SystemPermissions.PeopleUsersManage);
        Assert.Contains(permissions!, p => p.Code == SystemPermissions.ProductionMilkingRecord);

        var rolesResponse = await authedClient.GetAsync("/api/v1/people/roles");
        rolesResponse.EnsureSuccessStatusCode();
        var roles = await rolesResponse.Content.ReadFromJsonAsync<List<RoleDto>>();

        Assert.NotNull(roles);
        Assert.Contains(roles!, r => r.Code == SystemRoles.Admin);
        Assert.Contains(roles!, r => r.Code == SystemRoles.Registrar);
    }

    [Fact]
    public async Task CreateCustomRole_Succeeds()
    {
        var authedClient = await CreateAuthenticatedAdminClientAsync();

        var roleCode = $"custom-role-{Guid.NewGuid():N}";
        var response = await authedClient.PostAsJsonAsync("/api/v1/people/roles", new
        {
            code = roleCode,
            name = "Rol Personalizado",
            description = "Descripción del rol personalizado"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
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
            role = "veterinarian"
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
            role = "veterinarian"
        });

        var anonymousAttempt = await _client.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Intruso",
            email = "intruso@finca.ec",
            password = "WhateverPassword123!",
            role = "admin"
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
            role = "registrar"
        });
        registerResponse.EnsureSuccessStatusCode();
        var userId = (await registerResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var deactivateResponse = await authedClient.PostAsync($"/api/v1/people/users/{userId}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/people/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task DeactivateUser_RevokesAlreadyIssuedToken()
    {
        var authedClient = await CreateAuthenticatedAdminClientAsync();

        var email = "sesion-activa@finca.ec";
        var password = "CorrectPassword123!";
        var registerResponse = await authedClient.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Sesión Activa",
            email,
            password,
            role = "registrar"
        });
        registerResponse.EnsureSuccessStatusCode();
        var userId = (await registerResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/people/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResultDto>();

        using var sessionClient = factory.CreateClient();
        sessionClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);

        var beforeResponse = await sessionClient.GetAsync("/api/v1/inventory/items");
        Assert.NotEqual(HttpStatusCode.Unauthorized, beforeResponse.StatusCode);

        var deactivateResponse = await authedClient.PostAsync($"/api/v1/people/users/{userId}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);

        var afterResponse = await sessionClient.GetAsync("/api/v1/inventory/items");
        Assert.Equal(HttpStatusCode.Unauthorized, afterResponse.StatusCode);
    }

    [Fact]
    public async Task DeactivateUser_LastActiveAdmin_IsRejected()
    {
        var authedClient = await CreateAuthenticatedAdminClientAsync();

        var meResponse = await authedClient.GetAsync("/api/v1/people/users");
        meResponse.EnsureSuccessStatusCode();
        var users = await meResponse.Content.ReadFromJsonAsync<List<UserDto>>();
        var admin = users!.Single(u => u.Email == AdminEmail);

        var response = await authedClient.PostAsync($"/api/v1/people/users/{admin.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    [Fact]
    public async Task CreateSpecies_AsRegistrar_IsForbidden()
    {
        var registrarClient = await CreateAuthenticatedRegistrarClientAsync();

        var response = await registrarClient.PostAsJsonAsync("/api/v1/species", new { name = "Porcino", gestationDays = 114 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateSpecies_AsAdmin_Succeeds()
    {
        var authedClient = await CreateAuthenticatedAdminClientAsync();

        var response = await authedClient.PostAsJsonAsync("/api/v1/species", new { name = $"Bovino-{Guid.NewGuid()}", gestationDays = 283 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedRegistrarClientAsync()
    {
        var authedAdminClient = await CreateAuthenticatedAdminClientAsync();

        const string email = "registrador-rbac@finca.ec";
        const string password = "CorrectPassword123!";

        await authedAdminClient.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Registrador RBAC",
            email,
            password,
            role = "registrar"
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/people/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResultDto>();
        Assert.NotNull(login);

        var registrarClient = factory.CreateClient();
        registrarClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return registrarClient;
    }

    private sealed record CreatedId(Guid Id);
}
