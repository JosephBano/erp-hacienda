using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hato.Modules.People.Domain;
using Hato.Modules.People.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// Feature 0008 Commit 2 integration tests (T2.1 - T2.10):
/// Verifies the normative permission matrix row-by-row on REST routes:
/// - 401 when anonymous
/// - 403 when user lacks permission
/// - 200/201/204 when user has permission
/// - Revoked permissions are denied even with existing JWT
/// Runs against real PostgreSQL.
/// </summary>
[Collection(SyncCollection.Name)]
public class RestEndpointsPermissionTests(SyncApiFactory factory)
{
    private async Task<(Guid speciesId, Guid animalId)> SeedSpeciesAndAnimalAsync()
    {
        var admin = await SyncTestContext.AdminClientAsync(factory);
        var speciesResponse = await admin.PostAsJsonAsync("/api/v1/species", new
        {
            name = $"Species-{Guid.NewGuid():N}",
            isMilkable = true,
            gestationDays = 280
        });
        speciesResponse.EnsureSuccessStatusCode();
        var speciesJson = await speciesResponse.Content.ReadFromJsonAsync<JsonElement>();
        var speciesId = speciesJson.GetProperty("id").GetGuid();

        var animalResponse = await admin.PostAsJsonAsync("/api/v1/animals", new
        {
            speciesId,
            sex = "Female"
        });
        animalResponse.EnsureSuccessStatusCode();
        var animalJson = await animalResponse.Content.ReadFromJsonAsync<JsonElement>();
        var animalId = animalJson.GetProperty("id").GetGuid();

        return (speciesId, animalId);
    }

    [Fact]
    public async Task AnonymousRequests_Return401_AcrossAllSecuredEndpoints()
    {
        var anon = factory.CreateClient();
        var randomId = Guid.NewGuid();

        // Animals
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/v1/animals", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/animals")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/v1/animals/{randomId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync($"/api/v1/animals/{randomId}/identifiers", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/v1/animals/{randomId}/individual-state")).StatusCode);

        // Milking
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/v1/milking-sessions", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/milking-sessions")).StatusCode);

        // Breeding
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/v1/breeding/semen-straws", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/breeding/semen-straws")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/v1/breeding/services", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/v1/breeding/pregnancy-checks", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/breeding/pregnancies/active")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/v1/breeding/birthings", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/breeding/birthings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/v1/breeding/weanings", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync($"/api/v1/breeding/cohorts/{randomId}/wean", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync($"/api/v1/breeding/cohorts/{randomId}/classify-by-weight", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/v1/breeding/pedigree/{randomId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/v1/breeding/kpis/dams/{randomId}")).StatusCode);

        // Tasks
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/alerts")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/api/v1/alerts/generate", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync($"/api/v1/alerts/{randomId}/dismiss", content: null)).StatusCode);

        // AnimalEvents
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync($"/api/v1/animals/{randomId}/events", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/v1/animals/{randomId}/events")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/v1/animals/{randomId}/withdrawal-periods")).StatusCode);

        // PlausibilityRanges
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/plausibility-ranges")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/v1/plausibility-ranges", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PatchAsJsonAsync($"/api/v1/plausibility-ranges/{randomId}/bounds", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.DeleteAsync($"/api/v1/plausibility-ranges/{randomId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync($"/api/v1/plausibility-ranges/{randomId}/activate", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/v1/plausibility-ranges/evaluate", new { })).StatusCode);
    }

    [Fact]
    public async Task AnimalsEndpoints_PermissionEnforcement()
    {
        var (speciesId, animalId) = await SeedSpeciesAndAnimalAsync();

        // 1. User without livestock.animals.write (has read only)
        var readerOnly = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "anim-read-only", SystemPermissions.LivestockAnimalsRead);

        // Write actions return 403
        var postAnimalResp = await readerOnly.Client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Female" });
        Assert.Equal(HttpStatusCode.Forbidden, postAnimalResp.StatusCode);

        var postIdentResp = await readerOnly.Client.PostAsJsonAsync($"/api/v1/animals/{animalId}/identifiers", new
        {
            type = "FarmTag",
            value = $"T-{Guid.NewGuid():N}"[..8],
            validFrom = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        Assert.Equal(HttpStatusCode.Forbidden, postIdentResp.StatusCode);

        // Read actions succeed (200)
        var getAnimalsResp = await readerOnly.Client.GetAsync("/api/v1/animals");
        Assert.Equal(HttpStatusCode.OK, getAnimalsResp.StatusCode);

        var getAnimalResp = await readerOnly.Client.GetAsync($"/api/v1/animals/{animalId}");
        Assert.Equal(HttpStatusCode.OK, getAnimalResp.StatusCode);

        var getStateResp = await readerOnly.Client.GetAsync($"/api/v1/animals/{animalId}/individual-state");
        Assert.Equal(HttpStatusCode.OK, getStateResp.StatusCode);

        // 2. User with no livestock permissions at all (e.g. only MilkingRead)
        var milkingUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "anim-no-perm", SystemPermissions.ProductionMilkingRead);

        Assert.Equal(HttpStatusCode.Forbidden, (await milkingUser.Client.GetAsync("/api/v1/animals")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await milkingUser.Client.GetAsync($"/api/v1/animals/{animalId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await milkingUser.Client.GetAsync($"/api/v1/animals/{animalId}/individual-state")).StatusCode);

        // 3. User with livestock.animals.write
        var writerUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "anim-writer", SystemPermissions.LivestockAnimalsWrite, SystemPermissions.LivestockAnimalsRead);

        var createResp = await writerUser.Client.PostAsJsonAsync("/api/v1/animals", new { speciesId, sex = "Male" });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var createIdentResp = await writerUser.Client.PostAsJsonAsync($"/api/v1/animals/{animalId}/identifiers", new
        {
            type = "FarmTag",
            value = $"T-{Guid.NewGuid():N}"[..8],
            validFrom = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        Assert.Equal(HttpStatusCode.Created, createIdentResp.StatusCode);
    }

    [Fact]
    public async Task MilkingEndpoints_PermissionEnforcement()
    {
        var (speciesId, animalId) = await SeedSpeciesAndAnimalAsync();

        // 1. User without production.milking.record (has read only)
        var readerOnly = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "milk-read-only", SystemPermissions.ProductionMilkingRead);

        var postResp = await readerOnly.Client.PostAsJsonAsync("/api/v1/milking-sessions", new
        {
            date = DateOnly.FromDateTime(DateTime.UtcNow),
            shift = "Morning",
            recordedBy = "Reader",
            totalLiters = 20.0m
        });
        Assert.Equal(HttpStatusCode.Forbidden, postResp.StatusCode);

        var getResp = await readerOnly.Client.GetAsync("/api/v1/milking-sessions");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        // 2. User without any milking permission
        var unrelatedUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "milk-no-perm", SystemPermissions.LivestockAnimalsRead);

        Assert.Equal(HttpStatusCode.Forbidden, (await unrelatedUser.Client.GetAsync("/api/v1/milking-sessions")).StatusCode);

        // 3. User with production.milking.record
        var recorderUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "milk-recorder", SystemPermissions.ProductionMilkingRecord);

        var postOkResp = await recorderUser.Client.PostAsJsonAsync("/api/v1/milking-sessions", new
        {
            date = DateOnly.FromDateTime(DateTime.UtcNow),
            shift = "Morning",
            recordedBy = "Recorder",
            totalLiters = 25.0m
        });
        Assert.Equal(HttpStatusCode.Created, postOkResp.StatusCode);
    }

    [Fact]
    public async Task BreedingEndpoints_PermissionEnforcement()
    {
        var (speciesId, damId) = await SeedSpeciesAndAnimalAsync();

        // 1. User with breeding.events.read only
        var readerOnly = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "breed-read-only", SystemPermissions.BreedingEventsRead);

        // Read endpoints succeed
        Assert.Equal(HttpStatusCode.OK, (await readerOnly.Client.GetAsync("/api/v1/breeding/semen-straws")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await readerOnly.Client.GetAsync("/api/v1/breeding/pregnancies/active")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await readerOnly.Client.GetAsync("/api/v1/breeding/birthings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await readerOnly.Client.GetAsync($"/api/v1/breeding/kpis/dams/{damId}")).StatusCode);

        // Record endpoints return 403
        var strawResp = await readerOnly.Client.PostAsJsonAsync("/api/v1/breeding/semen-straws", new
        {
            code = $"STR-{Guid.NewGuid():N}"[..12],
            bullName = "Bull A",
            breedId = Guid.NewGuid(),
            quantity = 5
        });
        Assert.Equal(HttpStatusCode.Forbidden, strawResp.StatusCode);

        var randomId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await readerOnly.Client.PostAsJsonAsync($"/api/v1/breeding/cohorts/{randomId}/wean", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await readerOnly.Client.PostAsJsonAsync($"/api/v1/breeding/cohorts/{randomId}/classify-by-weight", new { })).StatusCode);

        // 2. User with no breeding permissions
        var unrelatedUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "breed-no-perm", SystemPermissions.TasksRead);

        Assert.Equal(HttpStatusCode.Forbidden, (await unrelatedUser.Client.GetAsync("/api/v1/breeding/semen-straws")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await unrelatedUser.Client.GetAsync("/api/v1/breeding/pregnancies/active")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await unrelatedUser.Client.GetAsync("/api/v1/breeding/birthings")).StatusCode);

        // 3. User with breeding.events.record
        var recordUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "breed-recorder", SystemPermissions.BreedingEventsRecord);

        var strawCreateResp = await recordUser.Client.PostAsJsonAsync("/api/v1/breeding/semen-straws", new
        {
            code = $"STR-{Guid.NewGuid():N}"[..12],
            bullName = "Bull B",
            breedId = Guid.NewGuid(),
            quantity = 10
        });
        Assert.Equal(HttpStatusCode.Created, strawCreateResp.StatusCode);
    }

    [Fact]
    public async Task TasksEndpoints_PermissionEnforcement()
    {
        // 1. User with tasks.read only
        var readerOnly = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "tasks-read-only", SystemPermissions.TasksRead);

        var getResp = await readerOnly.Client.GetAsync("/api/v1/alerts");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var generateResp = await readerOnly.Client.PostAsync("/api/v1/alerts/generate", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, generateResp.StatusCode);

        var dismissResp = await readerOnly.Client.PostAsync($"/api/v1/alerts/{Guid.NewGuid()}/dismiss", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, dismissResp.StatusCode);

        // 2. User without tasks permissions
        var unrelatedUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "tasks-no-perm", SystemPermissions.LivestockAnimalsRead);

        Assert.Equal(HttpStatusCode.Forbidden, (await unrelatedUser.Client.GetAsync("/api/v1/alerts")).StatusCode);

        // 3. User with tasks.manage
        var managerUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "tasks-manager", SystemPermissions.TasksManage);

        var genOkResp = await managerUser.Client.PostAsync("/api/v1/alerts/generate", content: null);
        Assert.Equal(HttpStatusCode.OK, genOkResp.StatusCode);
    }

    [Fact]
    public async Task AnimalEventsEndpoints_PermissionEnforcement()
    {
        var (speciesId, animalId) = await SeedSpeciesAndAnimalAsync();

        // 1. User with livestock.animals.read only
        var readerOnly = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "events-read-only", SystemPermissions.LivestockAnimalsRead);

        var getEventsResp = await readerOnly.Client.GetAsync($"/api/v1/animals/{animalId}/events");
        Assert.Equal(HttpStatusCode.OK, getEventsResp.StatusCode);

        var getWithdrawalsResp = await readerOnly.Client.GetAsync($"/api/v1/animals/{animalId}/withdrawal-periods");
        Assert.Equal(HttpStatusCode.OK, getWithdrawalsResp.StatusCode);

        var postEventResp = await readerOnly.Client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = "Weighing",
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "Reader",
            payloadJson = "{\"weightKg\": 280}"
        });
        Assert.Equal(HttpStatusCode.Forbidden, postEventResp.StatusCode);

        // 2. User with no livestock permissions
        var unrelatedUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "events-no-perm", SystemPermissions.ProductionMilkingRead);

        Assert.Equal(HttpStatusCode.Forbidden, (await unrelatedUser.Client.GetAsync($"/api/v1/animals/{animalId}/events")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await unrelatedUser.Client.GetAsync($"/api/v1/animals/{animalId}/withdrawal-periods")).StatusCode);

        // 3. User with livestock.animals.write
        var writerUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "events-writer", SystemPermissions.LivestockAnimalsWrite);

        var postOkResp = await writerUser.Client.PostAsJsonAsync($"/api/v1/animals/{animalId}/events", new
        {
            eventType = "Weighing",
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "Writer",
            payloadJson = "{\"weightKg\": 310}"
        });
        Assert.Equal(HttpStatusCode.Created, postOkResp.StatusCode);
    }

    [Fact]
    public async Task PlausibilityRangesEndpoints_PermissionEnforcement()
    {
        var (speciesId, _) = await SeedSpeciesAndAnimalAsync();

        // Authenticated user without livestock.animals.write can read and evaluate
        var readerOnly = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "plaus-read-only", SystemPermissions.LivestockAnimalsRead);

        var getResp = await readerOnly.Client.GetAsync("/api/v1/plausibility-ranges");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var evalResp = await readerOnly.Client.PostAsJsonAsync("/api/v1/plausibility-ranges/evaluate", new
        {
            speciesId,
            magnitude = "milk_daily_liters",
            value = 15.0m
        });
        Assert.Equal(HttpStatusCode.OK, evalResp.StatusCode);

        // Mutating actions return 403 without livestock.animals.write
        var postResp = await readerOnly.Client.PostAsJsonAsync("/api/v1/plausibility-ranges", new
        {
            speciesId,
            magnitude = "milk_daily_liters",
            plausibleMin = 2m,
            plausibleMax = 35m,
            absoluteMin = 0m,
            absoluteMax = 50m
        });
        Assert.Equal(HttpStatusCode.Forbidden, postResp.StatusCode);

        var randomId = Guid.NewGuid();
        var patchResp = await readerOnly.Client.PatchAsJsonAsync($"/api/v1/plausibility-ranges/{randomId}/bounds", new
        {
            plausibleMin = 3m,
            plausibleMax = 36m,
            absoluteMin = 0m,
            absoluteMax = 55m
        });
        Assert.Equal(HttpStatusCode.Forbidden, patchResp.StatusCode);

        var deleteResp = await readerOnly.Client.DeleteAsync($"/api/v1/plausibility-ranges/{randomId}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResp.StatusCode);

        var activateResp = await readerOnly.Client.PostAsync($"/api/v1/plausibility-ranges/{randomId}/activate", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, activateResp.StatusCode);

        // User with livestock.animals.write can mutate
        var writerUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "plaus-writer", SystemPermissions.LivestockAnimalsWrite);

        var createResp = await writerUser.Client.PostAsJsonAsync("/api/v1/plausibility-ranges", new
        {
            speciesId,
            magnitude = "milk_daily_liters",
            plausibleMin = 2m,
            plausibleMax = 35m,
            absoluteMin = 0m,
            absoluteMax = 50m
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var createdRange = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var rangeId = createdRange.GetProperty("id").GetGuid();

        var patchOkResp = await writerUser.Client.PatchAsJsonAsync($"/api/v1/plausibility-ranges/{rangeId}/bounds", new
        {
            plausibleMin = 3m,
            plausibleMax = 36m,
            absoluteMin = 0m,
            absoluteMax = 55m
        });
        Assert.Equal(HttpStatusCode.NoContent, patchOkResp.StatusCode);

        var deleteOkResp = await writerUser.Client.DeleteAsync($"/api/v1/plausibility-ranges/{rangeId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteOkResp.StatusCode);

        var activateOkResp = await writerUser.Client.PostAsync($"/api/v1/plausibility-ranges/{rangeId}/activate", content: null);
        Assert.Equal(HttpStatusCode.NoContent, activateOkResp.StatusCode);
    }

    [Fact]
    public async Task RevokedPermission_DeniedImmediately_EvenWithPreExistingJwt()
    {
        var (speciesId, _) = await SeedSpeciesAndAnimalAsync();

        // 1. Create a user with livestock.animals.write and obtain their JWT
        var testUser = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "revocation-test", SystemPermissions.LivestockAnimalsWrite);

        // 2. Verify write succeeds with initial JWT
        var initialWriteResp = await testUser.Client.PostAsJsonAsync("/api/v1/animals", new
        {
            speciesId,
            sex = "Female"
        });
        Assert.Equal(HttpStatusCode.Created, initialWriteResp.StatusCode);

        // 3. Revoke permission in the database: remove the user's role assignment
        using (var scope = factory.Services.CreateScope())
        {
            var peopleDb = scope.ServiceProvider.GetRequiredService<PeopleDbContext>();
            var userRoles = await peopleDb.UserRoles
                .Where(ur => ur.UserId == testUser.UserId)
                .ToListAsync();

            peopleDb.UserRoles.RemoveRange(userRoles);
            await peopleDb.SaveChangesAsync();
        }

        // 4. Verify that subsequent request WITH THE SAME UNEXPIRED JWT is now rejected with 403
        var revokedWriteResp = await testUser.Client.PostAsJsonAsync("/api/v1/animals", new
        {
            speciesId,
            sex = "Female"
        });
        Assert.Equal(HttpStatusCode.Forbidden, revokedWriteResp.StatusCode);
    }
}
