using Hato.SharedKernel;

namespace Hato.Modules.Livestock.UnitTests.SharedKernel;

public class EntityTests
{
    private sealed class TestEntity : Entity;

    [Fact]
    public void NewEntity_GetsSovereignInternalUuid()
    {
        var entity = new TestEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    [Fact]
    public void TwoEntities_GetDistinctIds()
    {
        Assert.NotEqual(new TestEntity().Id, new TestEntity().Id);
    }
}
