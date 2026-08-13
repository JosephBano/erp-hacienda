using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Modules.Inventory.IntegrationTests.Receptions;

/// <summary>
/// Auth + permission tests for the new POST /receptions endpoint and the now-permission-gated
/// POST /batches and POST /feed-stage. Mirrors the <c>JwtHatoApiFactory</c> pattern from the
/// Livestock suite (ADR-0025 PR2): skips <c>TestAuthHandler</c> so we can mint a JWT whose
/// principal carries an explicit permission set, then exercise the policy gate end-to-end.
/// </summary>
public class JwtInventoryApiFactory : InventoryApiFactory, IAsyncLifetime
{
    public const string AdminEmail = "inv-receptions-admin@hato.local";
    public const string AdminPassword = "InvReceptionsAdmin123!";
    public const string NoReceptionRoleCode = "no-inventory-receptions";

    public string AdminJwt { get; private set; } = string.Empty;

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

        // The base InventoryApiFactory only migrates Livestock/Production/Inventory
        // schemas; JWT-based tests need People too so the /auth/login endpoint can
        // issue a token. Migrate People here, against the same connection string.
        var peopleOptions = new DbContextOptionsBuilder<PeopleDbContext>()
            .UseNpgsql(ConnectionString, n => n.MigrationsHistoryTable("__ef_migrations_history", PeopleDbContext.Schema))
            .Options;
        await using (var peopleMigrate = new PeopleDbContext(peopleOptions))
        {
            await peopleMigrate.Database.MigrateAsync();
        }

        using var scope = Services.CreateScope();
        var peopleDb = scope.ServiceProvider.GetRequiredService<PeopleDbContext>();

        if (await peopleDb.Users.AnyAsync())
        {
            return;
        }

        var adminRole = await peopleDb.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstAsync(r => r.Code == SystemRoles.Admin);

        var admin = User.Create("Inventory Receptions Admin", AdminEmail, PasswordHasher.Hash(AdminPassword));
        admin.AddRole(adminRole);
        peopleDb.Users.Add(admin);

        // A role with no inventory.* permissions at all — exercises the 403 path.
        var noReceptionRole = Role.Create(NoReceptionRoleCode, "Sin recepciones", "Rol sin permisos de inventario", isSystem: false);
        peopleDb.Roles.Add(noReceptionRole);

        var uniqueEmail = $"no-receptions-{Guid.NewGuid():N}@finca.ec";
        var noReceptionUser = User.Create("No Reception User", uniqueEmail, PasswordHasher.Hash(AdminPassword));
        noReceptionUser.AddRole(noReceptionRole);
        peopleDb.Users.Add(noReceptionUser);

        await peopleDb.SaveChangesAsync();

        // Capture an admin JWT for setup calls.
        var anonClient = CreateClient();
        var loginResp = await anonClient.PostAsJsonAsync("/api/v1/people/auth/login",
            new { email = AdminEmail, password = AdminPassword });
        loginResp.EnsureSuccessStatusCode();
        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(loginBody);
        AdminJwt = loginBody!.Token;

        NoReceptionEmail = uniqueEmail;
    }

    public string NoReceptionEmail { get; private set; } = string.Empty;

    public HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AdminJwt);
        return client;
    }

    public async Task<HttpClient> CreateNoReceptionClientAsync()
    {
        var anonClient = CreateClient();
        var loginResp = await anonClient.PostAsJsonAsync("/api/v1/people/auth/login",
            new { email = NoReceptionEmail, password = AdminPassword });
        loginResp.EnsureSuccessStatusCode();
        var body = await loginResp.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(body);
        Assert.DoesNotContain(SystemPermissions.InventoryReceptionsManage, body!.Permissions);
        Assert.DoesNotContain(SystemPermissions.InventoryItemsManage, body.Permissions);

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        return client;
    }

    private sealed record LoginResult(string Token, DateTime ExpiresAt, Guid UserId, string FullName, string[] Roles, string[] Permissions);
}

public class RecordInventoryReceptionAuthApiTests(JwtInventoryApiFactory factory) : IClassFixture<JwtInventoryApiFactory>
{
    [Fact]
    public async Task PostReceptions_WithoutToken_Returns401()
    {
        var client = factory.CreateClient(); // no Authorization header

        var resp = await client.PostAsJsonAsync(
            $"/api/v1/inventory/items/{Guid.NewGuid()}/receptions", new
            {
                batchNumber = "LOT-1",
                quantity = 1m,
                unit = "kg",
                costPerUnit = 0m,
                receivedAt = DateTimeOffset.UtcNow.ToString("o"),
            });

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task PostReceptions_WithTokenButNoInventoryReceptionsManage_Returns403()
    {
        var noReceptionClient = await factory.CreateNoReceptionClientAsync();

        // First seed an item using the admin client so the no-permission user can target it.
        var adminClient = factory.CreateAdminClient();
        var itemId = await SeedItemAsync(adminClient);

        var resp = await noReceptionClient.PostAsJsonAsync(
            $"/api/v1/inventory/items/{itemId}/receptions", new
            {
                batchNumber = "LOT-403-A",
                quantity = 1m,
                unit = "kg",
                costPerUnit = 0m,
                receivedAt = DateTimeOffset.UtcNow.ToString("o"),
            });

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task PostReceptions_WithAdminPermissions_Succeeds()
    {
        var adminClient = factory.CreateAdminClient();
        var itemId = await SeedItemAsync(adminClient);

        var resp = await adminClient.PostAsJsonAsync(
            $"/api/v1/inventory/items/{itemId}/receptions", new
            {
                batchNumber = "LOT-OK-A",
                quantity = 10m,
                unit = "kg",
                costPerUnit = 0.5m,
                receivedAt = DateTimeOffset.UtcNow.ToString("o"),
                supplierLabel = "Agro XYZ",
            });

        resp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PostBatches_WithTokenButNoInventoryItemsManage_Returns403()
    {
        var noReceptionClient = await factory.CreateNoReceptionClientAsync();
        var adminClient = factory.CreateAdminClient();
        var itemId = await SeedItemAsync(adminClient);

        // The legacy endpoint now also requires inventory.items.manage; the
        // no-permission user has neither, so the request is 403.
        var resp = await noReceptionClient.PostAsJsonAsync(
            $"/api/v1/inventory/items/{itemId}/batches", new
            {
                batchNumber = "LOT-403-B",
                quantity = 1m,
                costPerUnit = 0m,
                expirationDate = (DateOnly?)null,
            });

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, resp.StatusCode);
    }

    private static async Task<Guid> SeedItemAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/inventory/items", new
        {
            name = $"Auth-Target-{Guid.NewGuid():N}",
            category = "Feed",
            unit = "kg",
            minStock = 0m,
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<CreatedId>();
        return body!.Id;
    }

    private sealed record CreatedId(Guid Id);
}
