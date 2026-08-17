using System.Text.Json;
using Hato.Modules.People.Domain;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// Commit 3 — Preñeces y servicios en el pull (feature/field-app-parto-redesign).
/// Gated by <c>breeding.events.read</c>. Tests T3.6, T3.7, T3.8, T3.9.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncPullBreedingTests(SyncApiFactory factory)
{
    [Fact]
    public async Task Pull_DeliversPregnanciesAndBreedingServices_ToUserWithBreedingReadPermission()
    {
        var admin = await SyncTestContext.CreateAsync(factory, "breeding-admin");
        var speciesId = await admin.CreateSpeciesAsync("Bovino", gestationDays: 283);
        var damId = await admin.CreateAnimalAsync(speciesId, sex: "Female");
        var sireId = await admin.CreateAnimalAsync(speciesId, sex: "Male");

        var serviceId = await admin.RegisterBreedingServiceAsync(damId, sireAnimalId: sireId, serviceType: "Natural");
        await admin.RecordPregnancyCheckAsync(serviceId, result: "Positive");

        var limited = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "with-breeding-read",
            SystemPermissions.LivestockAnimalsRead,
            SystemPermissions.BreedingEventsRead);

        var pull = await limited.PullAsync(collections: "pregnancies,breedingServices");
        var pregnancies = pull.GetProperty("collections").GetProperty("pregnancies").EnumerateArray().ToList();
        var services = pull.GetProperty("collections").GetProperty("breedingServices").EnumerateArray().ToList();

        var serviceRow = Assert.Single(services, s => s.GetProperty("id").GetGuid() == serviceId);
        Assert.Equal(damId, serviceRow.GetProperty("damId").GetGuid());
        Assert.Equal("Natural", serviceRow.GetProperty("serviceType").GetString());
        Assert.Equal(sireId, serviceRow.GetProperty("sireAnimalId").GetGuid());
        Assert.False(serviceRow.GetProperty("isDeleted").GetBoolean());

        var pregnancyRow = Assert.Single(pregnancies, p => p.GetProperty("damId").GetGuid() == damId);
        Assert.Equal(serviceId, pregnancyRow.GetProperty("serviceId").GetGuid());
        Assert.Equal("Active", pregnancyRow.GetProperty("status").GetString());
        Assert.False(pregnancyRow.GetProperty("isDeleted").GetBoolean());
    }

    [Fact]
    public async Task Pull_HidesPregnanciesAndBreedingServices_FromUserWithoutBreedingReadPermission_EvenWhenRequestedByName()
    {
        var admin = await SyncTestContext.CreateAsync(factory, "breeding-hide-admin");
        var speciesId = await admin.CreateSpeciesAsync("Bovino", gestationDays: 283);
        var damId = await admin.CreateAnimalAsync(speciesId, sex: "Female");
        var sireId = await admin.CreateAnimalAsync(speciesId, sex: "Male");

        var serviceId = await admin.RegisterBreedingServiceAsync(damId, sireAnimalId: sireId, serviceType: "Natural");
        await admin.RecordPregnancyCheckAsync(serviceId, result: "Positive");

        var limited = await SyncTestContext.CreateWithPermissionsAsync(
            factory, "no-breeding-read",
            SystemPermissions.LivestockAnimalsRead);

        var pull = await limited.PullAsync(collections: "pregnancies,breedingServices", batchSize: 1000);

        Assert.Empty(pull.GetProperty("collections").GetProperty("pregnancies").EnumerateArray());
        Assert.Empty(pull.GetProperty("collections").GetProperty("breedingServices").EnumerateArray());
    }

    [Fact]
    public async Task Pull_BirthingRecord_MovesPregnancyToCompleted_AndReflectsInNextPull()
    {
        var context = await SyncTestContext.CreateAsync(factory, "birth-pregnancy-complete");
        var speciesId = await context.CreateSpeciesAsync("Bovino", gestationDays: 283);
        var damId = await context.CreateAnimalAsync(speciesId, sex: "Female");
        var sireId = await context.CreateAnimalAsync(speciesId, sex: "Male");

        var serviceId = await context.RegisterBreedingServiceAsync(damId, sireAnimalId: sireId, serviceType: "Natural");
        await context.RecordPregnancyCheckAsync(serviceId, result: "Positive");

        // Initial pull to get cursor and verify active pregnancy
        var initialPull = await context.PullAsync(collections: "pregnancies");
        var initialPregnancies = initialPull.GetProperty("collections").GetProperty("pregnancies").EnumerateArray().ToList();
        var pregnancyRow = Assert.Single(initialPregnancies, p => p.GetProperty("damId").GetGuid() == damId);
        Assert.Equal("Active", pregnancyRow.GetProperty("status").GetString());
        var cursor = initialPull.GetProperty("cursor").GetString();

        // Push a birth for this dam
        var birthResult = await context.PushAsync("recordBirth", new
        {
            damId,
            birthDate = DateOnly.FromDateTime(DateTime.UtcNow),
            difficulty = "Normal",
            bornAlive = 1,
            offspring = new[] { new { sex = "F", farmTag = (string?)null, birthWeightKg = 31.0m } },
        });
        Assert.Equal("Accepted", birthResult.GetProperty("status").GetString());

        // Next pull with cursor should deliver the updated pregnancy marked as Completed
        var nextPull = await context.PullAsync(since: cursor, collections: "pregnancies");
        var updatedPregnancies = nextPull.GetProperty("collections").GetProperty("pregnancies").EnumerateArray().ToList();
        var completedRow = Assert.Single(updatedPregnancies, p => p.GetProperty("id").GetGuid() == pregnancyRow.GetProperty("id").GetGuid());
        Assert.Equal("Completed", completedRow.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Pull_CursorAdvancesProperly_WithBreedingCollections()
    {
        var context = await SyncTestContext.CreateAsync(factory, "breeding-cursor-adv");
        var speciesId = await context.CreateSpeciesAsync("Bovino", gestationDays: 283);
        var damId = await context.CreateAnimalAsync(speciesId, sex: "Female");
        var sireId = await context.CreateAnimalAsync(speciesId, sex: "Male");

        var initialPull = await context.PullAsync(collections: "breedingServices,pregnancies");
        var cursor = initialPull.GetProperty("cursor").GetString();

        // Add breeding service and check
        var serviceId = await context.RegisterBreedingServiceAsync(damId, sireAnimalId: sireId, serviceType: "Natural");
        await context.RecordPregnancyCheckAsync(serviceId, result: "Positive");

        // Pull with previous cursor delivers the new service and pregnancy
        var secondPull = await context.PullAsync(since: cursor, collections: "breedingServices,pregnancies");
        var services = secondPull.GetProperty("collections").GetProperty("breedingServices").EnumerateArray().ToList();
        var pregnancies = secondPull.GetProperty("collections").GetProperty("pregnancies").EnumerateArray().ToList();

        Assert.Contains(services, s => s.GetProperty("id").GetGuid() == serviceId);
        Assert.Contains(pregnancies, p => p.GetProperty("damId").GetGuid() == damId);

        var nextCursor = secondPull.GetProperty("cursor").GetString();
        Assert.NotEqual(cursor, nextCursor);

        // Third pull with nextCursor does not redeliver them
        var thirdPull = await context.PullAsync(since: nextCursor, collections: "breedingServices,pregnancies");
        var servicesThird = thirdPull.GetProperty("collections").GetProperty("breedingServices").EnumerateArray().ToList();
        var pregnanciesThird = thirdPull.GetProperty("collections").GetProperty("pregnancies").EnumerateArray().ToList();

        Assert.DoesNotContain(servicesThird, s => s.GetProperty("id").GetGuid() == serviceId);
        Assert.DoesNotContain(pregnanciesThird, p => p.GetProperty("damId").GetGuid() == damId);
    }
}
