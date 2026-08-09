using System.Net;
using System.Net.Http.Json;
using Hato.Modules.Livestock.Application.AnimalGroups;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

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

    // === ADR-0025 PR1: Update / Deactivate / Activate / ChangeTrackingMode handlers ===
    // Endpoint coverage arrives in PR2; the handler-level coverage lives here so PR1
    // is green on its own.

    [Fact]
    public async Task UpdateAnimalGroupCommand_UpdatesAllFields_AndResolvesSpeciesName()
    {
        var (groupId, originalSpeciesId) = await CreateGroupWithSpeciesAsync("Update Target");
        var newSpeciesId = await CreateSpeciesAsync($"Bovino-Update-{Guid.NewGuid():N}");

        var dto = await SendAsync(new UpdateAnimalGroupCommand(
            groupId,
            Name: "Update Target v2",
            Description: "Renombrado y reasignado",
            SpeciesId: newSpeciesId));

        Assert.Equal("Update Target v2", dto.Name);
        Assert.Equal("Renombrado y reasignado", dto.Description);
        Assert.Equal(newSpeciesId, dto.SpeciesId);
        Assert.NotNull(dto.SpeciesName);
        Assert.Equal(0, dto.LiveHeadCount); // no members, no disposals
    }

    [Fact]
    public async Task UpdateAnimalGroupCommand_OnNonexistentGroup_Throws()
    {
        var missing = Guid.NewGuid();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => SendAsync(new UpdateAnimalGroupCommand(
            missing, "Whatever", null, null)));
    }

    [Fact]
    public async Task UpdateAnimalGroupValidator_RejectsEmptyName()
    {
        var (groupId, _) = await CreateGroupWithSpeciesAsync("Validator Target");

        var validator = new UpdateAnimalGroupValidator();
        var result = validator.Validate(new UpdateAnimalGroupCommand(groupId, "", null, null));
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task DeactivateAnimalGroupCommand_FlipsIsActive_AndIsIdempotent()
    {
        var (groupId, _) = await CreateGroupWithSpeciesAsync("Deactivate Target");

        await SendAsync(new DeactivateAnimalGroupCommand(groupId));
        var afterFirst = await GetByIdAsync(groupId);
        Assert.False(afterFirst.IsActive);

        await SendAsync(new DeactivateAnimalGroupCommand(groupId));
        var afterSecond = await GetByIdAsync(groupId);
        Assert.False(afterSecond.IsActive);
    }

    [Fact]
    public async Task ActivateAnimalGroupCommand_RestoresIsActive_FromFalse()
    {
        var (groupId, _) = await CreateGroupWithSpeciesAsync("Activate Target");
        await SendAsync(new DeactivateAnimalGroupCommand(groupId));

        await SendAsync(new ActivateAnimalGroupCommand(groupId));

        var after = await GetByIdAsync(groupId);
        Assert.True(after.IsActive);
    }

    [Fact]
    public async Task ActivateAnimalGroupCommand_OnNonexistentGroup_Throws()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            SendAsync(new ActivateAnimalGroupCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task ChangeAnimalGroupTrackingModeCommand_OnEmptyGroup_Succeeds()
    {
        var (groupId, _) = await CreateGroupWithSpeciesAsync("ChangeMode Target");

        var dto = await SendAsync(new ChangeAnimalGroupTrackingModeCommand(groupId, TrackingMode.Headcount));

        Assert.Equal(TrackingMode.Headcount, dto.TrackingMode);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task ChangeAnimalGroupTrackingModeCommand_SameMode_IsNoOp()
    {
        var (groupId, _) = await CreateGroupWithSpeciesAsync("NoOp Target", TrackingMode.Headcount);

        var dto = await SendAsync(new ChangeAnimalGroupTrackingModeCommand(groupId, TrackingMode.Headcount));

        Assert.Equal(TrackingMode.Headcount, dto.TrackingMode);
    }

    [Fact]
    public async Task ChangeAnimalGroupTrackingModeCommand_OnInactiveGroup_ThrowsDomainException()
    {
        var (groupId, _) = await CreateGroupWithSpeciesAsync("Inactive ChangeMode Target");
        await SendAsync(new DeactivateAnimalGroupCommand(groupId));

        // Request a different mode so the handler actually attempts the change
        // (same-mode is a no-op and skips the IsActive guard by design).
        await Assert.ThrowsAsync<DomainException>(() =>
            SendAsync(new ChangeAnimalGroupTrackingModeCommand(groupId, TrackingMode.Headcount)));
    }

    [Fact]
    public async Task ChangeAnimalGroupTrackingModeCommand_OnGroupWithActiveMember_ThrowsStateException()
    {
        var (groupId, _) = await CreateGroupWithSpeciesAsync("WithMember ChangeMode Target");
        var animalId = await CreateAnimalAsync();

        await SendAsync(new AddGroupMemberCommand(groupId, animalId, new DateOnly(2026, 8, 1)));

        var ex = await Assert.ThrowsAsync<Hato.Modules.Livestock.Domain.Exceptions.AnimalGroupStateException>(() =>
            SendAsync(new ChangeAnimalGroupTrackingModeCommand(groupId, TrackingMode.Headcount)));

        Assert.Contains("miembros activos", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangeAnimalGroupTrackingModeCommand_OnGroupWithEvent_ThrowsStateException()
    {
        var (groupId, _) = await CreateGroupWithSpeciesAsync("WithEvent ChangeMode Target");

        // Seed a group event directly via MediatR so the test exercises the handler
        // (RecordGroupEvent) the same way the API does, without coupling to the
        // HTTP request shape.
        await SendAsync(new Hato.Modules.Livestock.Application.Events.RecordGroupEventCommand(
            GroupId: groupId,
            EventType: EventType.GroupWeightSorting,
            OccurredAt: new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero),
            RecordedBy: "test-suite",
            PayloadJson: "{\"head_count\":10,\"avg_kg\":30.0}"));

        var ex = await Assert.ThrowsAsync<Hato.Modules.Livestock.Domain.Exceptions.AnimalGroupStateException>(() =>
            SendAsync(new ChangeAnimalGroupTrackingModeCommand(groupId, TrackingMode.Headcount)));

        Assert.Contains("eventos registrados", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAnimalGroupsQuery_PopulatesLiveHeadCountAndSpeciesName()
    {
        var speciesId = await CreateSpeciesAsync($"Bovino-List-{Guid.NewGuid():N}");
        var groupId = await CreateGroupOnlyAsync(speciesId: speciesId, name: "List Target");

        // Seed 1 active member, no disposals → LiveHeadCount should be 1.
        var animalId = await CreateAnimalAsync();
        await SendAsync(new AddGroupMemberCommand(groupId, animalId, new DateOnly(2026, 8, 1)));

        var groups = await SendAsync(new GetAnimalGroupsQuery(IncludeInactive: false));

        var found = groups.Single(g => g.Id == groupId);
        Assert.Equal(1, found.LiveHeadCount);
        Assert.NotNull(found.SpeciesName);
        Assert.Equal(speciesId, found.SpeciesId);
    }

    // === Helpers ===
    // Following the MortalityCauseApiTests.cs precedent: resolve ISender from a fresh
    // scope and dispatch directly. Two overloads: typed response (queries, commands
    // returning a value) and untyped (MediatR's IRequest marker for void-returning
    // commands like AddGroupMember / DeactivateAnimalGroup).

    private async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        using var scope = factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(request);
    }

    private async Task SendAsync(IRequest request)
    {
        using var scope = factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(request);
    }

    private async Task<AnimalGroupDto> GetByIdAsync(Guid id)
    {
        return await SendAsync(new GetAnimalGroupByIdQuery(id));
    }

    private async Task<Guid> CreateSpeciesAsync(string? name = null)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/species", new
        {
            name = name ?? $"Bovino-{Guid.NewGuid():N}",
            gestationDays = 283,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private async Task<Guid> CreateAnimalAsync()
    {
        var speciesId = await CreateSpeciesAsync();
        var response = await _client.PostAsJsonAsync("/api/v1/animals", new
        {
            speciesId,
            sex = Sex.Female,
            birthDate = (DateOnly?)null,
            breedId = (Guid?)null,
            categoryId = (Guid?)null,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private async Task<Guid> CreateGroupOnlyAsync(Guid? speciesId = null, string? name = null, TrackingMode mode = TrackingMode.Individual)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/animal-groups", new
        {
            name = name ?? $"Group-{Guid.NewGuid():N}",
            description = (string?)null,
            speciesId = speciesId,
            trackingMode = mode,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private async Task<(Guid groupId, Guid speciesId)> CreateGroupWithSpeciesAsync(string name, TrackingMode mode = TrackingMode.Individual)
    {
        var speciesId = await CreateSpeciesAsync();
        var groupId = await CreateGroupOnlyAsync(speciesId, name, mode);
        return (groupId, speciesId);
    }

    private sealed record CreatedId(Guid Id);
}
