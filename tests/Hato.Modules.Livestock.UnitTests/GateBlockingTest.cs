using Xunit;

namespace Hato.Modules.Livestock.UnitTests;

public class GateBlockingTest
{
    [Fact]
    public void DeliberateFailureToTestGate()
    {
        Assert.Fail("Deliberate failure to test merge gate blocking per spec 0011 E2E-3");
    }
}
