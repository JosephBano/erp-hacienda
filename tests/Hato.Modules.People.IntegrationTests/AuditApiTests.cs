using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hato.Modules.People.Application.Audit;
using Hato.Modules.People.Application.Auth;
using Hato.Modules.People.Application.Users;
using Xunit;

namespace Hato.Modules.People.IntegrationTests;

public class AuditApiTests(PeopleApiFactory factory) : IClassFixture<PeopleApiFactory>
{
    private const string AdminEmail = "admin-audit@finca.ec";
    private const string AdminPassword = "SecurePassword123!";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetAuditLogs_WithAdminToken_ReturnsPaginatedAuditEntries()
    {
        var authedClient = await CreateAuthenticatedAdminClientAsync();

        // Perform an action that generates audit log (register user)
        await authedClient.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Empleado Auditado",
            email = $"auditado-{Guid.NewGuid():N}@finca.ec",
            password = "SecurePassword123!",
            role = "registrar"
        });

        var response = await authedClient.GetAsync("/api/v1/people/audit?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();

        var pagedLogs = await response.Content.ReadFromJsonAsync<PagedAuditLogsDto>();
        Assert.NotNull(pagedLogs);
        Assert.True(pagedLogs!.TotalCount > 0);
        Assert.NotEmpty(pagedLogs.Items);
    }

    [Fact]
    public async Task GetAuditLogs_WithoutPermission_ReturnsForbiddenOrUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/people/audit");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedAdminClientAsync()
    {
        await _client.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Administrador Auditoría",
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
}
