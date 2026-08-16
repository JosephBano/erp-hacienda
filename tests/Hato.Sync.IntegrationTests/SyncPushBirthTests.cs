using System.Text.Json;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// Births are the one field flow where the client creates a brand new animal offline
/// (docs/planes/fase-3/spec.md sec.3.B). The calf has to arrive on the server exactly once and with its
/// genealogy intact — a calf without a dam is a silently corrupted pedigree that nobody
/// notices until someone asks who its mother was, years later.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncPushBirthTests(SyncApiFactory factory)
{
    [Fact]
    public async Task Push_RecordBirth_CreatesOffspringLinkedToItsDam()
    {
        var context = await SyncTestContext.CreateAsync(factory, "birth-genealogy");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var damId = await context.CreateAnimalAsync(speciesId);

        var result = await context.PushAsync(
            "recordBirth",
            new
            {
                damId,
                birthDate = DateOnly.FromDateTime(DateTime.UtcNow),
                difficulty = "Normal",
                bornAlive = 1,
                offspring = new[] { new { sex = "F", farmTag = (string?)null, birthWeightKg = 32.5m } },
            });

        Assert.Equal("Accepted", result.GetProperty("status").GetString());

        var offspring = await context.FindOffspringOfAsync(damId);
        Assert.Single(offspring);
    }

    [Fact]
    public async Task Push_RecordBirthSentTwice_DoesNotCreateTwoCalves()
    {
        var context = await SyncTestContext.CreateAsync(factory, "birth-doubletap");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var damId = await context.CreateAnimalAsync(speciesId);

        var operationId = Guid.NewGuid();
        var payload = new
        {
            damId,
            birthDate = DateOnly.FromDateTime(DateTime.UtcNow),
            difficulty = "Normal",
            bornAlive = 1,
            offspring = new[] { new { sex = "F", farmTag = (string?)null, birthWeightKg = (decimal?)null } },
        };

        var first = await context.PushAsync("recordBirth", payload, operationId);
        var second = await context.PushAsync("recordBirth", payload, operationId);

        Assert.Equal("Accepted", first.GetProperty("status").GetString());
        Assert.Equal("Duplicate", second.GetProperty("status").GetString());

        var offspring = await context.FindOffspringOfAsync(damId);
        Assert.Single(offspring);
    }

    /// <summary>
    /// A birth witnessed in the paddock often has no pregnancy record behind it — natural
    /// mating, nobody logged the service. The employee who was there knows the sire, so
    /// the app lets them declare it, and that declaration has to reach the calf. If the
    /// server only ever resolved the sire from a pregnancy, the field-recorded birth would
    /// come back fatherless with no error to show for it.
    /// </summary>
    [Fact]
    public async Task Push_RecordBirthWithDeclaredSire_LinksTheCalfToItsFather()
    {
        var context = await SyncTestContext.CreateAsync(factory, "birth-sire");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var damId = await context.CreateAnimalAsync(speciesId);
        var sireId = await context.CreateAnimalAsync(speciesId, sex: "Male");

        var result = await context.PushAsync(
            "recordBirth",
            new
            {
                damId,
                sireAnimalId = sireId,
                birthDate = DateOnly.FromDateTime(DateTime.UtcNow),
                difficulty = "Normal",
                bornAlive = 1,
                offspring = new[] { new { sex = "F", farmTag = (string?)null, birthWeightKg = (decimal?)null } },
            });

        Assert.Equal("Accepted", result.GetProperty("status").GetString());

        var offspring = await context.FindOffspringOfAsync(damId);
        var calf = await context.FindAsync("animals", Assert.Single(offspring));

        Assert.Equal(sireId, calf.GetProperty("fatherAnimalId").GetGuid());
    }

    [Fact]
    public async Task Push_RecordBirthOfTwins_ConvergesWithBothCalves()
    {
        var context = await SyncTestContext.CreateAsync(factory, "birth-twins");
        var speciesId = await context.CreateSpeciesAsync("Ovino");
        var damId = await context.CreateAnimalAsync(speciesId);

        var result = await context.PushAsync(
            "recordBirth",
            new
            {
                damId,
                birthDate = DateOnly.FromDateTime(DateTime.UtcNow),
                difficulty = "Normal",
                bornAlive = 2,
                offspring = new[]
                {
                    new { sex = "F", farmTag = (string?)null, birthWeightKg = (decimal?)null },
                    new { sex = "M", farmTag = (string?)null, birthWeightKg = (decimal?)null },
                },
            });

        Assert.Equal("Accepted", result.GetProperty("status").GetString());

        var offspring = await context.FindOffspringOfAsync(damId);
        Assert.Equal(2, offspring.Count);
    }
}
