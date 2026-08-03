using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Hato.Modules.People.Application.Auth;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// Per-test helper: provisions an isolated user, logs it in and exposes the small set of
/// API calls the synchronization scenarios need. Every test gets its own user and its own
/// species so that tests sharing the container never observe each other's rows.
/// </summary>
public class SyncTestContext
{
    private readonly SyncApiFactory _factory;

    private SyncTestContext(SyncApiFactory factory, HttpClient client, Guid userId)
    {
        _factory = factory;
        Client = client;
        UserId = userId;
    }

    public HttpClient Client { get; }

    public Guid UserId { get; }

    private const string SharedAdminEmail = "sync-suite-admin@finca.ec";
    private const string Password = "SecurePassword123!";

    private static readonly SemaphoreSlim BootstrapLock = new(1, 1);
    private static bool _bootstrapped;

    public static async Task<SyncTestContext> CreateAsync(SyncApiFactory factory, string scenario)
    {
        var admin = await GetBootstrapAdminAsync(factory);

        var email = $"{scenario}-{Guid.NewGuid():N}@finca.ec";
        var create = await admin.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = $"Usuario {scenario}",
            email,
            password = Password,
            role = "admin",
        });
        create.EnsureSuccessStatusCode();

        var login = await LoginAsync(factory, email, Password);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        return new SyncTestContext(factory, client, login.UserId);
    }

    /// <summary>
    /// The first user of a fresh database registers itself without a token; every later
    /// registration needs an authenticated admin. This creates that first user exactly
    /// once for the whole suite and hands back a session for it.
    /// </summary>
    private static async Task<HttpClient> GetBootstrapAdminAsync(SyncApiFactory factory)
    {
        await BootstrapLock.WaitAsync();
        try
        {
            if (!_bootstrapped)
            {
                var anonymous = factory.CreateClient();
                var response = await anonymous.PostAsJsonAsync("/api/v1/people/users", new
                {
                    fullName = "Admin Suite Sync",
                    email = SharedAdminEmail,
                    password = Password,
                    role = "admin",
                });
                response.EnsureSuccessStatusCode();
                _bootstrapped = true;
            }
        }
        finally
        {
            BootstrapLock.Release();
        }

        var login = await LoginAsync(factory, SharedAdminEmail, Password);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        return client;
    }

    private static async Task<LoginResultDto> LoginAsync(SyncApiFactory factory, string email, string password)
    {
        var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/v1/people/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var login = await response.Content.ReadFromJsonAsync<LoginResultDto>();
        Assert.NotNull(login);
        return login!;
    }

    public async Task<Guid> CreateSpeciesAsync(string name)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/species", new { name = $"{name}-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    public async Task<Guid> CreateAnimalAsync(Guid speciesId, string sex = "Female")
    {
        var response = await Client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    public async Task<Guid> CreateGroupAsync(string name)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/animal-groups", new
        {
            name = $"{name}-{Guid.NewGuid():N}",
            description = (string?)null,
            speciesId = (Guid?)null,
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    public async Task AddMemberAsync(Guid groupId, Guid animalId)
    {
        var response = await Client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/members", new
        {
            animalId,
            joinedAt = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveMemberAsync(Guid groupId, Guid animalId)
    {
        var response = await Client.DeleteAsync($"/api/v1/animal-groups/{groupId}/members/{animalId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<JsonElement> PullAsync(string? since = null, string? collections = null, int? batchSize = null)
    {
        var query = new List<string>();
        if (since is not null) query.Add($"since={Uri.EscapeDataString(since)}");
        if (collections is not null) query.Add($"collections={Uri.EscapeDataString(collections)}");
        if (batchSize is not null) query.Add($"batchSize={batchSize}");

        var url = "/api/v1/sync/pull" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);

        var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
