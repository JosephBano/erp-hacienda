using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// Scenario 8 of docs/spec/plan-0001-fase-3/spec.md sec.2.2: "dos dispositivos editan el mismo campo → LWW
/// aplicado y entrada en la bitácora de conflictos." Animal's mutable fields (breed,
/// category, birth date) are the "Datos de Animales" example ADR-0008 names for the
/// editable-entity LWW strategy — everything else in the sync protocol is append-only
/// events, where two devices simply cannot collide on the same field.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncPushLwwConflictTests(SyncApiFactory factory)
{
    [Fact]
    public async Task TwoDevicesEditingTheSameField_TheLaterDeclaredEditWins()
    {
        var context = await SyncTestContext.CreateAsync(factory, "lww-later-wins");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId);

        // Both devices last saw the animal freshly created — its LastEditedAt baseline
        // is null (never edited) at this point.
        var breedFromDeviceA = await context.CreateBreedAsync(speciesId);
        var breedFromDeviceB = await context.CreateBreedAsync(speciesId);

        var earlier = DateTimeOffset.UtcNow.AddHours(-3);
        var later = DateTimeOffset.UtcNow.AddHours(-1);

        // Device A pushes first (it regained signal sooner) but its edit happened earlier
        // in the field.
        var resultA = await PushUpdateAsync(context, animalId, breedFromDeviceA, knownUpdatedAt: null, occurredAt: earlier);
        Assert.Equal("Accepted", resultA.GetProperty("status").GetString());

        // Device B still believes the animal is in its pristine, never-edited state
        // (it pulled before A's edit landed), but its own edit happened later.
        var resultB = await PushUpdateAsync(context, animalId, breedFromDeviceB, knownUpdatedAt: null, occurredAt: later);
        Assert.Equal("Accepted", resultB.GetProperty("status").GetString());

        var animal = await context.FindAsync("animals", animalId);
        Assert.Equal(breedFromDeviceB, animal.GetProperty("breedId").GetGuid());
    }

    [Fact]
    public async Task ConflictingEdit_IsRecordedInTheConflictLog()
    {
        var context = await SyncTestContext.CreateAsync(factory, "lww-logged");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId);

        var earlier = DateTimeOffset.UtcNow.AddHours(-3);
        var later = DateTimeOffset.UtcNow.AddHours(-1);

        await PushUpdateAsync(context, animalId, await context.CreateBreedAsync(speciesId), knownUpdatedAt: null, occurredAt: earlier);
        await PushUpdateAsync(context, animalId, await context.CreateBreedAsync(speciesId), knownUpdatedAt: null, occurredAt: later);

        var conflicts = await GetConflictsAsync(context);
        var forThisAnimal = conflicts.Where(c => c.GetProperty("entityId").GetGuid() == animalId).ToList();

        Assert.Contains(forThisAnimal, c => c.GetProperty("fieldName").GetString() == "BreedId");
        Assert.Contains(forThisAnimal, c => c.GetProperty("resolution").GetString() == "ClientWon");
    }

    /// <summary>
    /// The losing edit still gets a place: its value shows up as the attempted one, so an
    /// admin reviewing the tray can see exactly what was overwritten and by what.
    /// </summary>
    [Fact]
    public async Task LosingEdit_IsVisibleInTheConflictLogAsTheAttemptedValue()
    {
        var context = await SyncTestContext.CreateAsync(factory, "lww-attempted");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId);

        var losingBreed = await context.CreateBreedAsync(speciesId);
        var winningBreed = await context.CreateBreedAsync(speciesId);

        var later = DateTimeOffset.UtcNow.AddHours(-1);
        var earlier = DateTimeOffset.UtcNow.AddHours(-3);

        // Pushed in this order, but B's declared edit moment is *later* than A's, so B
        // should win even though A's push reaches the server second.
        await PushUpdateAsync(context, animalId, winningBreed, knownUpdatedAt: null, occurredAt: later);
        await PushUpdateAsync(context, animalId, losingBreed, knownUpdatedAt: null, occurredAt: earlier);

        var animal = await context.FindAsync("animals", animalId);
        Assert.Equal(winningBreed, animal.GetProperty("breedId").GetGuid());

        var conflicts = await GetConflictsAsync(context);
        var breedConflict = conflicts.First(c =>
            c.GetProperty("entityId").GetGuid() == animalId
            && c.GetProperty("fieldName").GetString() == "BreedId");

        Assert.Equal("ServerWon", breedConflict.GetProperty("resolution").GetString());
        Assert.Equal(losingBreed.ToString(), breedConflict.GetProperty("attemptedValue").GetString());
        Assert.Equal(winningBreed.ToString(), breedConflict.GetProperty("serverValue").GetString());
    }

    /// <summary>
    /// A device that correctly reports the state it saw (because it pulled after the
    /// other device's edit already landed) is not a conflict at all — it is a normal,
    /// sequential edit, and must not appear in the tray.
    /// </summary>
    [Fact]
    public async Task SequentialEditsWithAnUpToDateBaseline_ProduceNoConflict()
    {
        var context = await SyncTestContext.CreateAsync(factory, "lww-sequential");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId);

        var firstEditAt = DateTimeOffset.UtcNow.AddHours(-2);
        await PushUpdateAsync(context, animalId, await context.CreateBreedAsync(speciesId), knownUpdatedAt: null, occurredAt: firstEditAt);

        // This device pulled after the first edit, so it correctly reports the animal's
        // new baseline before making its own edit.
        var afterFirstEdit = await context.FindAsync("animals", animalId);
        var knownBaseline = afterFirstEdit.GetProperty("lastEditedAt").GetDateTimeOffset();

        var secondBreed = await context.CreateBreedAsync(speciesId);
        var result = await PushUpdateAsync(
            context, animalId, secondBreed, knownUpdatedAt: knownBaseline, occurredAt: DateTimeOffset.UtcNow.AddHours(-1));

        Assert.Equal("Accepted", result.GetProperty("status").GetString());

        var conflicts = await GetConflictsAsync(context);
        Assert.DoesNotContain(conflicts, c => c.GetProperty("entityId").GetGuid() == animalId);

        var animal = await context.FindAsync("animals", animalId);
        Assert.Equal(secondBreed, animal.GetProperty("breedId").GetGuid());
    }

    /// <summary>Re-sending the exact same values that are already stored is not a conflict, even inside a stale window.</summary>
    [Fact]
    public async Task ReplayingAnAlreadyAppliedValue_DoesNotLogAConflict()
    {
        var context = await SyncTestContext.CreateAsync(factory, "lww-noop-replay");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId);
        var breedId = await context.CreateBreedAsync(speciesId);

        var firstAt = DateTimeOffset.UtcNow.AddHours(-2);
        await PushUpdateAsync(context, animalId, breedId, knownUpdatedAt: null, occurredAt: firstAt);

        // A second device, still on its stale baseline, happens to push the very same
        // breed another employee already agreed on — nothing to reconcile.
        await PushUpdateAsync(context, animalId, breedId, knownUpdatedAt: null, occurredAt: DateTimeOffset.UtcNow.AddHours(-1));

        var conflicts = await GetConflictsAsync(context);
        Assert.DoesNotContain(conflicts, c => c.GetProperty("entityId").GetGuid() == animalId);
    }

    [Fact]
    public async Task DirectPanelEdit_WithNoKnownBaseline_AlwaysAppliesWithoutConflict()
    {
        var context = await SyncTestContext.CreateAsync(factory, "lww-panel-edit");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId);
        var breedId = await context.CreateBreedAsync(speciesId);

        var response = await context.Client.PutAsJsonAsync($"/api/v1/animals/{animalId}", new
        {
            breedId,
            categoryId = (Guid?)null,
            birthDate = (DateOnly?)null,
        });

        response.EnsureSuccessStatusCode();

        var animal = await context.FindAsync("animals", animalId);
        Assert.Equal(breedId, animal.GetProperty("breedId").GetGuid());

        var conflicts = await GetConflictsAsync(context);
        Assert.DoesNotContain(conflicts, c => c.GetProperty("entityId").GetGuid() == animalId);
    }

    private static async Task<JsonElement> PushUpdateAsync(
        SyncTestContext context, Guid animalId, Guid breedId, DateTimeOffset? knownUpdatedAt, DateTimeOffset occurredAt) =>
        await context.PushAsync(
            "updateAnimal",
            new
            {
                animalId,
                breedId,
                categoryId = (Guid?)null,
                birthDate = (DateOnly?)null,
                knownUpdatedAt,
            },
            occurredAt: occurredAt);

    private static async Task<List<JsonElement>> GetConflictsAsync(SyncTestContext context)
    {
        var response = await context.Client.GetAsync("/api/v1/sync/conflicts");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.EnumerateArray().ToList();
    }
}
