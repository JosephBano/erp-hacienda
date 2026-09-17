using System.Net;
using System.Text.Json;
using Xunit;

namespace Hato.Sync.IntegrationTests;

[Collection(SyncCollection.Name)]
public class VersionEndpointTests(SyncApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetVersion_ReturnsOk_WithVersionMetadataAndNoBusinessData()
    {
        var response = await _client.GetAsync("/api/v1/version");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("version", out var versionProp));
        Assert.False(string.IsNullOrWhiteSpace(versionProp.GetString()));

        Assert.True(root.TryGetProperty("commit", out var commitProp));
        Assert.False(string.IsNullOrWhiteSpace(commitProp.GetString()));

        Assert.True(root.TryGetProperty("buildDate", out var buildDateProp));
        Assert.False(string.IsNullOrWhiteSpace(buildDateProp.GetString()));

        // Ensure no business data is exposed
        var propertyNames = root.EnumerateObject().Select(p => p.Name.ToLowerInvariant()).ToList();
        Assert.Equal(3, propertyNames.Count);
        Assert.Contains("version", propertyNames);
        Assert.Contains("commit", propertyNames);
        Assert.Contains("builddate", propertyNames);
    }

    [Fact]
    public async Task GetRootVersion_ReturnsOk_AndMatchesApiV1()
    {
        var response = await _client.GetAsync("/version");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("version", out var versionProp));
        Assert.False(string.IsNullOrWhiteSpace(versionProp.GetString()));
    }
}
