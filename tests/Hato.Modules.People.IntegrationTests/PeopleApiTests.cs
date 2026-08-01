using System.Net.Http.Json;
using Hato.Modules.People.Application.Users;
using Hato.Modules.People.Domain;
using Xunit;

namespace Hato.Modules.People.IntegrationTests;

public class PeopleApiTests(PeopleApiFactory factory) : IClassFixture<PeopleApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RegisterUser_AndGetUsers_RoundTripsThroughPostgres()
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/people/users", new
        {
            fullName = "Mayordomo Carlos",
            email = "carlos@finca.ec",
            password = "SecurePassword123!",
            role = UserRole.Registrar
        });
        registerResponse.EnsureSuccessStatusCode();
        var userId = (await registerResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var getUsersResponse = await _client.GetAsync("/api/v1/people/users");
        getUsersResponse.EnsureSuccessStatusCode();
        var users = await getUsersResponse.Content.ReadFromJsonAsync<List<UserDto>>();

        Assert.NotNull(users);
        Assert.NotEmpty(users!);
        var user = users!.First(u => u.Id == userId);
        Assert.Equal("Mayordomo Carlos", user.FullName);
        Assert.Equal("carlos@finca.ec", user.Email);
        Assert.Equal(UserRole.Registrar, user.Role);
        Assert.True(user.IsActive);
    }

    private sealed record CreatedId(Guid Id);
}
