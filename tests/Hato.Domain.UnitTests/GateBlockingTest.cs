using Xunit;

namespace Hato.Domain.UnitTests;

public class GateBlockingTest
{
    [Fact]
    public void DeliberateFailureToTestGate()
    {
        Assert.True(false, "Deliberate failure to test merge gate blocking per spec 0011 E2E-3");
    }
}
