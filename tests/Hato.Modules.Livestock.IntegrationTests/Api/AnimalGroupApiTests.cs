using System.Net;
using System.Net.Http.Json;
using Hato.Modules.Livestock.Application.AnimalGroups;
using Hato.Modules.Livestock.Domain;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

public class AnimalGroupApiTests(HatoApiFactory factory) : IClassFixture<HatoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateGroup_AddMember_AndRemoveMember_RoundTripsThroughPostgres()
    {
        // 1. Create Species and Animal
        var speciesResponse = await _client.PostAsJsonAsync("/api/v1/species", new { name = $"Bovino-{Guid.NewGuid():N}", gestationDays = 283 });
        var speciesId = (await speciesResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var animalResponse = await _client.PostAsJsonAsync("/api/v1/animals", new
        {
            speciesId,
            sex = Sex.Female,
            birthDate = (DateOnly?)null,
            breedId = (Guid?)null,
            categoryId = (Guid?)null,
        });
        var animalId = (await animalResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        // 2. Create Group
        var groupResponse = await _client.PostAsJsonAsync("/api/v1/animal-groups", new
        {
            name = "Vacas Lecheras - Ordeño 1",
            description = "Lote principal de ordeño matutino",
            speciesId = (Guid?)speciesId
        });
        groupResponse.EnsureSuccessStatusCode();
        var groupId = (await groupResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        // 3. Add Member to Group
        var addMemberResponse = await _client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/members", new
        {
            animalId,
            joinedAt = new DateOnly(2026, 1, 15)
        });
        addMemberResponse.EnsureSuccessStatusCode();

        // 4. Fetch Group
        var getResponse = await _client.GetAsync($"/api/v1/animal-groups/{groupId}");
        getResponse.EnsureSuccessStatusCode();
        var group = await getResponse.Content.ReadFromJsonAsync<AnimalGroupDto>();

        Assert.NotNull(group);
        Assert.Equal("Vacas Lecheras - Ordeño 1", group!.Name);
        Assert.Single(group.Memberships);
        Assert.Equal(animalId, group.Memberships[0].AnimalId);
        Assert.True(group.Memberships[0].IsActive);

        // 5. Remove Member from Group
        var removeResponse = await _client.DeleteAsync($"/api/v1/animal-groups/{groupId}/members/{animalId}?leftAt=2026-07-01");
        removeResponse.EnsureSuccessStatusCode();

        // 6. Fetch Group again and check closed membership
        var getUpdatedResponse = await _client.GetAsync($"/api/v1/animal-groups/{groupId}");
        getUpdatedResponse.EnsureSuccessStatusCode();
        var updatedGroup = await getUpdatedResponse.Content.ReadFromJsonAsync<AnimalGroupDto>();

        Assert.NotNull(updatedGroup);
        Assert.Single(updatedGroup!.Memberships);
        Assert.False(updatedGroup.Memberships[0].IsActive);
        Assert.Equal(new DateOnly(2026, 7, 1), updatedGroup.Memberships[0].LeftAt);
    }

    [Fact]
    public async Task CreateGroup_WithEmptyName_ReturnsBadRequestProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/animal-groups", new
        {
            name = "",
            description = "Invalido"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record CreatedId(Guid Id);
}
