using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using Xunit;

namespace Hato.Modules.Livestock.UnitTests;

public class SyncModelConventionTests
{
    [Fact]
    public void SyncableEntities_MustInheritFromAuditableEntity()
    {
        var syncableTypes = new[]
        {
            typeof(Animal),
            typeof(AnimalIdentifier),
            typeof(AnimalGroup),
            typeof(GroupMembership),
            typeof(Species),
            typeof(Breed),
            typeof(AnimalCategory)
        };

        foreach (var type in syncableTypes)
        {
            Assert.True(
                typeof(AuditableEntity).IsAssignableFrom(type),
                $"Entity type {type.Name} must inherit from AuditableEntity for sync capability.");
        }
    }
}
