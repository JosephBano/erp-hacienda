using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Hato.Modules.People.IntegrationTests;

public class BootstrapRaceApiTests(PeopleApiFactory factory) : IClassFixture<PeopleApiFactory>
{
    [Fact]
    public async Task RegisterUser_ConcurrentBootstrapAttempts_OnlyOneSucceeds()
    {
        var attempts = Enumerable.Range(0, 8).Select(i => factory.CreateClient().PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = $"Aspirante {i}",
            email = $"aspirante{i}@finca.ec",
            password = "SomePassword123!",
            role = "admin"
        }));

        var responses = await Task.WhenAll(attempts);

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Forbidden));
    }
}
