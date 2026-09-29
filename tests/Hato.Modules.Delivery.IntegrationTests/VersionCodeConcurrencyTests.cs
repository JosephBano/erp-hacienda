using Hato.Modules.Delivery.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Modules.Delivery.IntegrationTests;

public class VersionCodeConcurrencyTests(DeliveryApiFactory factory) : IClassFixture<DeliveryApiFactory>
{
    [Fact]
    public async Task ConcurrentReservations_ProducesMonotonicUniqueSequentialCodes()
    {
        const string testPackage = "com.joemandev.hatofieldapp.concurrency";
        const int concurrentCallers = 10;

        var tasks = Enumerable.Range(0, concurrentCallers).Select(async _ =>
        {
            using var scope = factory.Services.CreateScope();
            var reservator = scope.ServiceProvider.GetRequiredService<IVersionCodeReservator>();
            return await reservator.ReserveNextVersionCodeAsync(testPackage);
        });

        var results = await Task.WhenAll(tasks);

        // Assert all 10 version codes are unique
        var distinctCodes = results.Distinct().ToList();
        Assert.Equal(concurrentCallers, distinctCodes.Count);

        // Sort them and assert strictly monotonic sequential sequence (each is previous + 1)
        distinctCodes.Sort();
        for (int i = 0; i < distinctCodes.Count - 1; i++)
        {
            Assert.Equal(distinctCodes[i] + 1, distinctCodes[i + 1]);
        }
    }
}
