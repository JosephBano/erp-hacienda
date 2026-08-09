using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hato.Modules.Livestock.Application.AnimalGroups;
using Hato.Modules.Livestock.Domain;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// Permission-specific HTTP tests for the animal-groups endpoints added by ADR-0025 PR2.
/// Lives in its own class with its own factory (<see cref="JwtHatoApiFactory"/>) because
/// the standard <see cref="HatoApiFactory"/> uses TestAuthHandler which always authenticates
/// as admin — making the <c>RequirePermission</c> gates invisible to tests.
/// </summary>
public class AnimalGroupAuthApiTests(JwtHatoApiFactory factory) : IClassFixture<JwtHatoApiFactory>
{
    private HttpClient AdminClient() => factory.CreateAdminClient();

    [Fact]
    public async Task WriteEndpoints_WithoutLivestockAnimalsWritePermission_Returns403()
    {
        // Build a fresh client authenticated as a user that has NO livestock.* permissions.
        // The TestAuthHandler authenticates as Admin (without the granular permission
        // claim), so the only path to a 403 here is to mint a JWT whose principal does
        // not carry `livestock.animals.write`.
        var noWriteClient = await CreateNoWriteClientAsync();

        // Use the admin client for setup (POST /species requires LivestockSpeciesManage
        // — the no-write user has zero livestock.* permissions, so /species 403s).
        var (groupId, _) = await CreateGroupWithSpeciesAsync(AdminClient(), "Forbidden Target");

        var putResp = await noWriteClient.PutAsJsonAsync($"/api/v1/animal-groups/{groupId}", new { name = "x", description = "y", speciesId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Forbidden, putResp.StatusCode);

        var delResp = await noWriteClient.DeleteAsync($"/api/v1/animal-groups/{groupId}");
        Assert.Equal(HttpStatusCode.Forbidden, delResp.StatusCode);

        var actResp = await noWriteClient.PostAsync($"/api/v1/animal-groups/{groupId}/activate", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, actResp.StatusCode);

        var patchResp = await noWriteClient.PatchAsJsonAsync($"/api/v1/animal-groups/{groupId}/tracking-mode", new { trackingMode = "Headcount" });
        Assert.Equal(HttpStatusCode.Forbidden, patchResp.StatusCode);
    }

    [Fact]
    public async Task PostCreateGroup_WithoutLivestockAnimalsWritePermission_Returns403()
    {
        // Covers the hardened POST / that PR2 also gates: gap preexistente.
        var noWriteClient = await CreateNoWriteClientAsync();

        var response = await noWriteClient.PostAsJsonAsync("/api/v1/animal-groups", new
        {
            name = "Forbidden Create",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetEndpoints_StillOpen_ForAuthenticatedWithoutPermission()
    {
        // Reading is open under RequireAuthorization() only — consistent with AnimalsEndpoints.
        var noWriteClient = await CreateNoWriteClientAsync();

        var listResp = await noWriteClient.GetAsync("/api/v1/animal-groups");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
    }

    // === Helpers ===

    private async Task<HttpClient> CreateNoWriteClientAsync()
    {
        // Seed the role + user directly via DbContext. Going through POST /users would
        // require the admin JWT to carry `people.users.manage` (the admin role does
        // carry it, but the seed here is more explicit and avoids coupling test setup
        // to the admin seed migration).
        using var scope = factory.Services.CreateScope();
        var peopleDb = scope.ServiceProvider.GetRequiredService<PeopleDbContext>();

        const string noWriteRoleCode = "no-write-test-role";
        const string noWritePassword = "NoWriteUser123!";

        // Idempotent role creation.
        var role = await peopleDb.Roles.FirstOrDefaultAsync(r => r.Code == noWriteRoleCode);
        if (role is null)
        {
            role = Role.Create(noWriteRoleCode, "No Write Test", "Role without livestock.animals.write", isSystem: false);
            peopleDb.Roles.Add(role);
            await peopleDb.SaveChangesAsync();
        }

        // Each test invocation gets its own user with a unique email so a previous run
        // does not collide.
        var uniqueEmail = $"no-write-{Guid.NewGuid():N}@finca.ec";
        var hash = PasswordHasher.Hash(noWritePassword);
        var user = User.Create("No-Write Test User", uniqueEmail, hash);
        user.AddRole(role);
        peopleDb.Users.Add(user);
        await peopleDb.SaveChangesAsync();

        // Login via the public endpoint — anonymous, no auth needed.
        var anonClient = factory.CreateClient();
        var userLogin = await anonClient.PostAsJsonAsync("/api/v1/people/auth/login", new { email = uniqueEmail, password = noWritePassword });
        userLogin.EnsureSuccessStatusCode();
        var userLoginBody = await userLogin.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(userLoginBody);
        Assert.DoesNotContain("livestock.animals.write", userLoginBody!.Permissions);

        var noWriteClient = factory.CreateClient();
        noWriteClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userLoginBody.Token);
        return noWriteClient;
    }

    private async Task<(Guid groupId, Guid speciesId)> CreateGroupWithSpeciesAsync(HttpClient client, string name)
    {
        // Use the admin client for setup so the writes that go through `RequirePermission`
        // land as 201/204, not 403. Then the test uses the no-write client for the actual
        // endpoint under test.
        var speciesId = await CreateSpeciesAsync(client);
        var response = await client.PostAsJsonAsync("/api/v1/animal-groups", new
        {
            name,
            description = (string?)null,
            speciesId = (Guid?)speciesId,
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreatedId>();
        return (created!.Id, speciesId);
    }

    private async Task<Guid> CreateSpeciesAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Bovino-{Guid.NewGuid():N}",
            gestationDays = 283,
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreatedId>();
        return created!.Id;
    }

    private sealed record CreatedId(Guid Id);
    private sealed record LoginResult(string Token, DateTime ExpiresAt, Guid UserId, string FullName, string[] Roles, string[] Permissions);
}
