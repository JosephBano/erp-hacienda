using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Hato.Api.Endpoints;
using Hato.Modules.Delivery.Application.Abstractions;
using Hato.Modules.Delivery.Domain;
using Hato.Modules.Delivery.Domain.Enums;
using Hato.Modules.People.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Hato.Modules.Delivery.IntegrationTests;

public class HeaderAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "HeaderTestAuth";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey("X-Test-Anonymous"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>();
        if (Request.Headers.TryGetValue("X-Test-UserId", out var userId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        }
        else
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        }

        if (Request.Headers.TryGetValue("X-Test-Role", out var role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
        }

        if (Request.Headers.TryGetValue("X-Test-Permissions", out var perms))
        {
            foreach (var p in perms.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim("permission", p));
            }
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

public class DeliveryAuthorizationTests(DeliveryApiFactory factory) : IClassFixture<DeliveryApiFactory>
{
    private HttpClient CreateCustomClient()
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(HeaderAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, HeaderAuthHandler>(
                        HeaderAuthHandler.SchemeName, _ => { });
            });
        }).CreateClient();
    }

    [Fact]
    public async Task UnauthenticatedRequests_AreDeniedWith401()
    {
        var client = CreateCustomClient();
        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        var buildResp = await client.GetAsync("/api/v1/mobile-build-requests");
        Assert.Equal(HttpStatusCode.Unauthorized, buildResp.StatusCode);

        var releasesResp = await client.GetAsync("/api/v1/mobile-releases");
        Assert.Equal(HttpStatusCode.Unauthorized, releasesResp.StatusCode);
    }

    [Fact]
    public async Task Download_StageRelease_RequiresBuildsManagePermission()
    {
        var client = CreateCustomClient();

        // 1. Seed a stage release and dummy file
        var stageReleaseId = Guid.NewGuid();
        var fileName = $"hato-1.0.0-stage-b999-{Guid.NewGuid():N}.apk";
        var dummyApkBytes = "FAKE_STAGE_APK"u8.ToArray();

        using (var scope = factory.Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<IArtifactStorage>();
            using var ms = new MemoryStream(dummyApkBytes);
            await storage.SaveArtifactAsync(fileName, ms);
        }

        await factory.ExecuteDbContextAsync(async db =>
        {
            var r = MobileRelease.CreateCandidate(
                buildRequestId: Guid.NewGuid(),
                channel: MobileReleaseChannels.Stage,
                version: "1.0.0",
                versionCode: 999,
                packageName: "com.joemandev.hatofieldapp.stage",
                targetApiUrl: "https://joemanserver.ts.net/api",
                commitSha: "sha-stage-test",
                artifactFileName: fileName,
                artifactSha256: new string('c', 64),
                artifactSizeBytes: dummyApkBytes.Length,
                signingCertificateFingerprint: "11:22:33:44",
                apiCompatibility: ">=1.0.0");

            stageReleaseId = r.Id;
            db.MobileReleases.Add(r);
            await db.SaveChangesAsync();
        });

        // 2. Client with ONLY delivery.releases.download tries to download stage release -> Denied (401 or 403)
        client.DefaultRequestHeaders.Remove("X-Test-Permissions");
        client.DefaultRequestHeaders.Add("X-Test-Permissions", SystemPermissions.DeliveryReleasesDownload);

        var downloadResp = await client.GetAsync($"/api/v1/mobile-releases/{stageReleaseId}/download");
        // Stage releases additionally require delivery.builds.manage; throws UnauthorizedAccessException mapped to 401
        Assert.True(downloadResp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);

        // 3. Client WITH BOTH delivery.releases.download AND delivery.builds.manage -> Allowed (200 OK)
        client.DefaultRequestHeaders.Remove("X-Test-Permissions");
        client.DefaultRequestHeaders.Add("X-Test-Permissions", $"{SystemPermissions.DeliveryReleasesDownload},{SystemPermissions.DeliveryBuildsManage}");

        var authorizedResp = await client.GetAsync($"/api/v1/mobile-releases/{stageReleaseId}/download");
        Assert.Equal(HttpStatusCode.OK, authorizedResp.StatusCode);
        var bytes = await authorizedResp.Content.ReadAsByteArrayAsync();
        Assert.Equal(dummyApkBytes, bytes);
    }
}
