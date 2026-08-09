using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// A second factory for tests that exercise the real JWT auth scheme rather than the
/// default TestAuthHandler-as-admin path. The standard auth tests in this suite need
/// TestAuthHandler (admin-everyone is convenient), but permission tests need a real
/// principal whose claims encode a specific (and possibly empty) permission set.
///
/// On <c>InitializeAsync</c>, this factory seeds a single admin user in the People
/// schema and logs in to capture a JWT that every authenticated test client can use.
/// </summary>
public class JwtHatoApiFactory : HatoApiFactory, IAsyncLifetime
{
    public const string AdminEmail = "permission-test-admin@hato.local";
    public const string AdminPassword = "PermissionTestAdmin123!";

    public string AdminJwt { get; private set; } = string.Empty;

    /// <summary>
    /// Override the base's auth setup so the default scheme is JWT instead of
    /// TestAuthHandler. We must re-derive the connection string setup rather than
    /// call <c>base.ConfigureWebHost</c>, because the base always adds TestAuth.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:HatoDb"] = ConnectionString,
            });
        });
        // Intentionally NOT calling AddTestAuthentication: this factory uses real JWT.
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await base.InitializeAsync();

        using var scope = Services.CreateScope();
        var peopleDb = scope.ServiceProvider.GetRequiredService<PeopleDbContext>();

        // Idempotent: only seed if no users exist (each fixture is a fresh DB so this
        // is effectively always true, but the check keeps things safe under retries).
        if (await peopleDb.Users.AnyAsync())
        {
            return;
        }

        var adminRole = await peopleDb.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstAsync(r => r.Code == "admin");
        var admin = User.Create("Permission Test Admin", AdminEmail, PasswordHasher.Hash(AdminPassword));
        admin.AddRole(adminRole);
        peopleDb.Users.Add(admin);
        await peopleDb.SaveChangesAsync();

        // Anonymous login (the /auth/login endpoint is anonymous). Capture the JWT
        // once; every CreateAdminClient reuses it.
        var anonClient = CreateClient();
        var loginResp = await anonClient.PostAsJsonAsync("/api/v1/people/auth/login",
            new { email = AdminEmail, password = AdminPassword });
        loginResp.EnsureSuccessStatusCode();

        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(loginBody);
        AdminJwt = loginBody!.Token;
    }

    public HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AdminJwt);
        return client;
    }

    private sealed record LoginResult(string Token, DateTime ExpiresAt, Guid UserId, string FullName, string[] Roles, string[] Permissions);
}
