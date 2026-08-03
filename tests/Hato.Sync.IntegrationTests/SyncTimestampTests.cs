using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Hato.Sync.IntegrationTests;

/// <summary>
/// The whole pull protocol is built on <c>created_at</c>/<c>updated_at</c>: a row without
/// a real timestamp can never be placed relative to a cursor, so it either floods every
/// pull or disappears from all of them. These tests pin the stamping itself.
/// </summary>
[Collection(SyncCollection.Name)]
public class SyncTimestampTests(SyncApiFactory factory)
{
    [Fact]
    public async Task CreatedAnimal_IsStampedWithRealCreationTime()
    {
        var context = await SyncTestContext.CreateAsync(factory, "stamp-create");
        var speciesId = await context.CreateSpeciesAsync("Bovino");

        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        var animalId = await context.CreateAnimalAsync(speciesId);
        var after = DateTimeOffset.UtcNow.AddMinutes(1);

        var pull = await context.PullAsync();
        var animal = FindById(pull, "animals", animalId);

        var createdAt = animal.GetProperty("createdAt").GetDateTimeOffset();

        Assert.InRange(createdAt, before, after);
    }

    [Fact]
    public async Task ModifiedMembership_IsStampedWithUpdateTime()
    {
        var context = await SyncTestContext.CreateAsync(factory, "stamp-update");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId);
        var groupId = await context.CreateGroupAsync("Lote de prueba");
        await context.AddMemberAsync(groupId, animalId);

        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        await context.RemoveMemberAsync(groupId, animalId);
        var after = DateTimeOffset.UtcNow.AddMinutes(1);

        var pull = await context.PullAsync();
        var membership = FindByAnimal(pull, "groupMemberships", animalId);

        var updatedAt = membership.GetProperty("updatedAt").GetDateTimeOffset();

        Assert.InRange(updatedAt, before, after);
    }

    private static JsonElement FindByAnimal(JsonElement pull, string collection, Guid animalId)
    {
        foreach (var row in pull.GetProperty("collections").GetProperty(collection).EnumerateArray())
        {
            if (row.GetProperty("animalId").GetGuid() == animalId)
            {
                return row;
            }
        }

        throw new Xunit.Sdk.XunitException($"La colección '{collection}' no contiene filas del animal {animalId}.");
    }

    internal static JsonElement FindById(JsonElement pull, string collection, Guid id)
    {
        var rows = pull.GetProperty("collections").GetProperty(collection).EnumerateArray();
        foreach (var row in rows)
        {
            if (row.GetProperty("id").GetGuid() == id)
            {
                return row;
            }
        }

        throw new Xunit.Sdk.XunitException(
            $"La colección '{collection}' no contiene el registro {id}. " +
            $"Registros presentes: {pull.GetProperty("collections").GetProperty(collection).GetArrayLength()}.");
    }
}
