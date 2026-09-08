using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Sync.IntegrationTests;

[Collection(SyncCollection.Name)]
public class SyncPushAssignAnimalIdentifierTests(SyncApiFactory factory)
{
    [Fact]
    public async Task Push_AssignAnimalIdentifier_CreatesActiveIdentifier()
    {
        var context = await SyncTestContext.CreateAsync(factory, "push-assign-tag");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId, "Female");

        var result = await context.PushAsync("assignAnimalIdentifier", new
        {
            animalId,
            type = "FarmTag",
            value = "TAG-101",
            validFrom = "2026-09-08",
        });

        Assert.Equal("Accepted", result.GetProperty("status").GetString());
        var resultRef = result.GetProperty("resultRef").GetString();
        Assert.False(string.IsNullOrWhiteSpace(resultRef));
        var identifierId = Guid.Parse(resultRef!);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ILivestockDbContext>();
        var identifiers = await dbContext.AnimalIdentifiers
            .Where(i => i.AnimalId == animalId)
            .ToListAsync();

        Assert.Single(identifiers);
        var identifier = identifiers[0];
        Assert.Equal(identifierId, identifier.Id);
        Assert.Equal(IdentifierType.FarmTag, identifier.Type);
        Assert.Equal("TAG-101", identifier.Value);
        Assert.True(identifier.IsActive);
        Assert.Null(identifier.ValidTo);
        Assert.Equal(new DateOnly(2026, 9, 8), identifier.ValidFrom);
    }

    [Fact]
    public async Task Push_SameAssignIdentifierOperationTwice_IsAcceptedOnceThenDuplicate()
    {
        var context = await SyncTestContext.CreateAsync(factory, "push-assign-dedupe-op");
        var speciesId = await context.CreateSpeciesAsync("Porcino");
        var animalId = await context.CreateAnimalAsync(speciesId, "Female");
        var operationId = Guid.NewGuid();

        var payload = new
        {
            animalId,
            type = "FarmTag",
            value = "TAG-201",
            validFrom = "2026-09-08",
        };

        var first = await context.PushAsync("assignAnimalIdentifier", payload, operationId);
        var second = await context.PushAsync("assignAnimalIdentifier", payload, operationId);

        Assert.Equal("Accepted", first.GetProperty("status").GetString());
        Assert.Equal("Duplicate", second.GetProperty("status").GetString());
        Assert.Equal(
            first.GetProperty("resultRef").GetString(),
            second.GetProperty("resultRef").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ILivestockDbContext>();
        var identifiers = await dbContext.AnimalIdentifiers
            .Where(i => i.AnimalId == animalId)
            .ToListAsync();

        Assert.Single(identifiers);
        Assert.Equal("TAG-201", identifiers[0].Value);
        Assert.True(identifiers[0].IsActive);
    }

    [Fact]
    public async Task Push_AssignDuplicateTagWithDifferentOperationId_IsIdempotentWithoutDuplicatingRow()
    {
        var context = await SyncTestContext.CreateAsync(factory, "push-assign-idempotent-cmd");
        var speciesId = await context.CreateSpeciesAsync("Porcino");
        var animalId = await context.CreateAnimalAsync(speciesId, "Female");

        var payload = new
        {
            animalId,
            type = "FarmTag",
            value = "TAG-SAME",
            validFrom = "2026-09-08",
        };

        // Two distinct pushes (different clientOperationId), same tag assignment
        var first = await context.PushAsync("assignAnimalIdentifier", payload, Guid.NewGuid());
        var second = await context.PushAsync("assignAnimalIdentifier", payload, Guid.NewGuid());

        Assert.Equal("Accepted", first.GetProperty("status").GetString());
        Assert.Equal("Accepted", second.GetProperty("status").GetString());
        Assert.Equal(
            first.GetProperty("resultRef").GetString(),
            second.GetProperty("resultRef").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ILivestockDbContext>();
        var identifiers = await dbContext.AnimalIdentifiers
            .Where(i => i.AnimalId == animalId)
            .ToListAsync();

        Assert.Single(identifiers);
        Assert.Equal("TAG-SAME", identifiers[0].Value);
        Assert.True(identifiers[0].IsActive);
    }

    [Fact]
    public async Task Push_ReplaceTag_ClosesPreviousTagAndPreservesHistory()
    {
        var context = await SyncTestContext.CreateAsync(factory, "push-replace-tag");
        var speciesId = await context.CreateSpeciesAsync("Bovino");
        var animalId = await context.CreateAnimalAsync(speciesId, "Female");

        // 1. Assign first tag
        var firstResult = await context.PushAsync("assignAnimalIdentifier", new
        {
            animalId,
            type = "FarmTag",
            value = "TAG-OLD",
            validFrom = "2026-08-01",
        });
        Assert.Equal("Accepted", firstResult.GetProperty("status").GetString());
        var oldId = Guid.Parse(firstResult.GetProperty("resultRef").GetString()!);

        // 2. Replace with new tag on a later date
        var secondResult = await context.PushAsync("assignAnimalIdentifier", new
        {
            animalId,
            type = "FarmTag",
            value = "TAG-NEW",
            validFrom = "2026-09-08",
        });
        Assert.Equal("Accepted", secondResult.GetProperty("status").GetString());
        var newId = Guid.Parse(secondResult.GetProperty("resultRef").GetString()!);

        Assert.NotEqual(oldId, newId);

        // 3. Verify history in database
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ILivestockDbContext>();
        var identifiers = await dbContext.AnimalIdentifiers
            .Where(i => i.AnimalId == animalId)
            .OrderBy(i => i.ValidFrom)
            .ToListAsync();

        Assert.Equal(2, identifiers.Count);

        var oldTag = identifiers.Single(i => i.Id == oldId);
        Assert.Equal("TAG-OLD", oldTag.Value);
        Assert.False(oldTag.IsActive);
        Assert.Equal(new DateOnly(2026, 8, 1), oldTag.ValidFrom);
        Assert.Equal(new DateOnly(2026, 9, 8), oldTag.ValidTo);

        var newTag = identifiers.Single(i => i.Id == newId);
        Assert.Equal("TAG-NEW", newTag.Value);
        Assert.True(newTag.IsActive);
        Assert.Equal(new DateOnly(2026, 9, 8), newTag.ValidFrom);
        Assert.Null(newTag.ValidTo);
    }
}
