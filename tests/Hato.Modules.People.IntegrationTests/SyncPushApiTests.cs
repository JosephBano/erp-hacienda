using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hato.Api.Sync;
using Hato.Modules.People.Application.Auth;
using Xunit;

namespace Hato.Modules.People.IntegrationTests;

public class SyncPushApiTests(PeopleApiFactory factory) : IClassFixture<PeopleApiFactory>
{
    private const string UserEmail = "push-user@finca.ec";
    private const string UserPassword = "SecurePassword123!";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Push_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/sync/push", new
        {
            deviceId = "test-device",
            operations = new object[] { }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Push_IdempotentOperation_ReturnsDuplicateOnRetry()
    {
        var authedClient = await CreateAuthenticatedClientAsync();

        var speciesRes = await authedClient.PostAsJsonAsync("/api/v1/species", new { name = "Bovino Sincronización" });
        speciesRes.EnsureSuccessStatusCode();

        var speciesJson = await speciesRes.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var speciesId = speciesJson.GetProperty("id").GetGuid();

        var clientOpId = Guid.NewGuid();
        var pushPayload = new
        {
            deviceId = "device-ios-01",
            operations = new[]
            {
                new
                {
                    clientOperationId = clientOpId,
                    operationType = "createAnimal",
                    occurredAt = DateTimeOffset.UtcNow,
                    payload = new
                    {
                        speciesId = speciesId,
                        sex = "Female",
                        birthDate = "2024-01-15"
                    }
                }
            }
        };

        // First Push
        var response1 = await authedClient.PostAsJsonAsync("/api/v1/sync/push", pushPayload);
        response1.EnsureSuccessStatusCode();

        var result1 = await response1.Content.ReadFromJsonAsync<PushSyncBatchResponseDto>();
        Assert.NotNull(result1);
        Assert.Equal(1, result1!.ProcessedCount);
        Assert.Equal("Accepted", result1.Results[0].Status);
        Assert.NotNull(result1.Results[0].ResultRef);

        // Duplicate Push (same clientOperationId)
        var response2 = await authedClient.PostAsJsonAsync("/api/v1/sync/push", pushPayload);
        response2.EnsureSuccessStatusCode();

        var result2 = await response2.Content.ReadFromJsonAsync<PushSyncBatchResponseDto>();
        Assert.NotNull(result2);
        Assert.Equal(1, result2!.ProcessedCount);
        Assert.Equal("Duplicate", result2.Results[0].Status);
        Assert.Equal(result1.Results[0].ResultRef, result2.Results[0].ResultRef);
    }

    [Fact]
    public async Task Push_PartiallyFailingBatch_ProcessesValidAndRejectsInvalid()
    {
        var authedClient = await CreateAuthenticatedClientAsync();

        var validOpId = Guid.NewGuid();
        var invalidOpId = Guid.NewGuid();

        var speciesRes = await authedClient.PostAsJsonAsync("/api/v1/species", new { name = "Porcino Push Test" });
        speciesRes.EnsureSuccessStatusCode();

        var speciesJson = await speciesRes.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var speciesId = speciesJson.GetProperty("id").GetGuid();

        var pushPayload = new
        {
            deviceId = "device-android-02",
            operations = new object[]
            {
                new
                {
                    clientOperationId = validOpId,
                    operationType = "createAnimal",
                    occurredAt = DateTimeOffset.UtcNow,
                    payload = new
                    {
                        speciesId = speciesId,
                        sex = "Male"
                    }
                },
                new
                {
                    clientOperationId = invalidOpId,
                    operationType = "createAnimal",
                    occurredAt = DateTimeOffset.UtcNow,
                    payload = new
                    {
                        speciesId = Guid.Empty, // Invalid!
                        sex = "Female"
                    }
                }
            }
        };

        var response = await authedClient.PostAsJsonAsync("/api/v1/sync/push", pushPayload);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PushSyncBatchResponseDto>();
        Assert.NotNull(result);
        Assert.Equal(2, result!.ProcessedCount);

        var validRes = result.Results.First(r => r.ClientOperationId == validOpId);
        Assert.Equal("Accepted", validRes.Status);
        Assert.NotNull(validRes.ResultRef);

        var invalidRes = result.Results.First(r => r.ClientOperationId == invalidOpId);
        Assert.Equal("Rejected", invalidRes.Status);
        Assert.NotNull(invalidRes.ErrorDetails);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        await _client.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Usuario Push Sync Admin",
            email = UserEmail,
            password = UserPassword,
            role = "admin"
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
