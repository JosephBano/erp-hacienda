using System.Net;
using System.Net.Http.Json;
using Hato.Api.Endpoints;
using Hato.Modules.Livestock.Application.AnimalGroups;
using Hato.Modules.Livestock.Application.Events;
using Hato.Modules.Livestock.Application.MortalityCauses;
using Hato.Modules.Livestock.Domain;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// ADR-0015: a lot by headcount never chooses which animal a group disposal refers to,
/// and closes every remaining membership in one shot only when the count reaches zero.
/// </summary>
public class RecordGroupEventApiTests(HatoApiFactory factory) : IClassFixture<HatoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<Guid> CreateSpeciesAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/species", new { name = $"Porcino-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private async Task<Guid> CreateAnimalAsync(Guid speciesId)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/animals", new
        {
            speciesId,
            sex = Sex.Male,
            birthDate = (DateOnly?)null,
            breedId = (Guid?)null,
            categoryId = (Guid?)null,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private async Task<(Guid GroupId, List<Guid> AnimalIds)> SeedHeadcountGroupAsync(int headcount)
    {
        var speciesId = await CreateSpeciesAsync();

        var groupResponse = await _client.PostAsJsonAsync("/api/v1/animal-groups", new
        {
            name = $"Engorde-{Guid.NewGuid():N}",
            description = (string?)null,
            speciesId,
            trackingMode = TrackingMode.Headcount,
        });
        groupResponse.EnsureSuccessStatusCode();
        var groupId = (await groupResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;

        var animalIds = new List<Guid>();
        for (var i = 0; i < headcount; i++)
        {
            var animalId = await CreateAnimalAsync(speciesId);
            animalIds.Add(animalId);

            var addMember = await _client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/members", new
            {
                animalId,
                joinedAt = new DateOnly(2026, 1, 1),
            });
            addMember.EnsureSuccessStatusCode();
        }

        return (groupId, animalIds);
    }

    [Fact]
    public async Task RecordGroupWeighing_PersistsAndIsReadableFromHistory()
    {
        var (groupId, _) = await SeedHeadcountGroupAsync(5);

        var response = await _client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/events", new
        {
            eventType = EventType.Weighing,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "capataz",
            payloadJson = "{\"sampleCount\":5,\"avgKg\":32.4,\"minKg\":28.1,\"maxKg\":36.9}",
        });
        response.EnsureSuccessStatusCode();

        var historyResponse = await _client.GetAsync($"/api/v1/animal-groups/{groupId}/events");
        historyResponse.EnsureSuccessStatusCode();
        var history = await historyResponse.Content.ReadFromJsonAsync<List<AnimalEventDto>>();

        Assert.NotNull(history);
        var recorded = Assert.Single(history!);
        Assert.Null(recorded.AnimalId);
        Assert.Equal(groupId, recorded.GroupId);
        Assert.Equal(EventType.Weighing, recorded.EventType);
    }

    [Fact]
    public async Task RecordGroupDisposal_WithoutAffectedCount_IsRejected()
    {
        var (groupId, _) = await SeedHeadcountGroupAsync(3);

        var response = await _client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/events", new
        {
            eventType = EventType.Disposal,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "capataz",
            payloadJson = "{\"causeId\":null}",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordGroupDisposal_Partial_LowersLiveHeadCount_AndClosesNoMembership()
    {
        var (groupId, _) = await SeedHeadcountGroupAsync(10);

        var response = await _client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/events", new
        {
            eventType = EventType.Disposal,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "capataz",
            payloadJson = "{\"count\":4,\"causeId\":null}",
            affectedCount = 4,
        });
        response.EnsureSuccessStatusCode();

        var countResponse = await _client.GetAsync($"/api/v1/animal-groups/{groupId}/live-head-count");
        countResponse.EnsureSuccessStatusCode();
        var body = await countResponse.Content.ReadFromJsonAsync<LiveHeadCountDto>();

        Assert.Equal(6, body!.LiveHeadCount);

        var groupResponse = await _client.GetAsync($"/api/v1/animal-groups/{groupId}");
        groupResponse.EnsureSuccessStatusCode();
        var group = await groupResponse.Content.ReadFromJsonAsync<AnimalGroupDto>();

        Assert.All(group!.Memberships, m => Assert.True(m.IsActive));
    }

    [Fact]
    public async Task RecordGroupDisposal_ReachingZero_ClosesAllMemberships_AndDisposesEveryAnimal()
    {
        var (groupId, animalIds) = await SeedHeadcountGroupAsync(6);

        var response = await _client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/events", new
        {
            eventType = EventType.Disposal,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "capataz",
            payloadJson = "{\"count\":6,\"causeId\":null}",
            affectedCount = 6,
        });
        response.EnsureSuccessStatusCode();

        var countResponse = await _client.GetAsync($"/api/v1/animal-groups/{groupId}/live-head-count");
        countResponse.EnsureSuccessStatusCode();
        var body = await countResponse.Content.ReadFromJsonAsync<LiveHeadCountDto>();
        Assert.Equal(0, body!.LiveHeadCount);

        var groupResponse = await _client.GetAsync($"/api/v1/animal-groups/{groupId}");
        groupResponse.EnsureSuccessStatusCode();
        var group = await groupResponse.Content.ReadFromJsonAsync<AnimalGroupDto>();
        Assert.All(group!.Memberships, m => Assert.False(m.IsActive));

        foreach (var animalId in animalIds)
        {
            var stateResponse = await _client.GetAsync($"/api/v1/animals/{animalId}/individual-state");
            stateResponse.EnsureSuccessStatusCode();
            var state = await stateResponse.Content.ReadFromJsonAsync<IndividualStateDto>();
            Assert.Equal(nameof(IndividualState.Disposed), state!.State);
        }
    }

    [Fact]
    public async Task RecordGroupDisposal_MoreThanLiveHeadCount_IsRejected()
    {
        var (groupId, _) = await SeedHeadcountGroupAsync(4);

        var response = await _client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/events", new
        {
            eventType = EventType.Disposal,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "capataz",
            payloadJson = "{\"count\":9,\"causeId\":null}",
            affectedCount = 9,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var countResponse = await _client.GetAsync($"/api/v1/animal-groups/{groupId}/live-head-count");
        countResponse.EnsureSuccessStatusCode();
        var body = await countResponse.Content.ReadFromJsonAsync<LiveHeadCountDto>();
        Assert.Equal(4, body!.LiveHeadCount);
    }

    [Fact]
    public async Task RecordGroupDisposal_SeveralPartialsReachingExactlyZero_ClosesOnTheLastOne()
    {
        var (groupId, _) = await SeedHeadcountGroupAsync(20);

        foreach (var count in new[] { 5, 8, 7 })
        {
            var response = await _client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/events", new
            {
                eventType = EventType.Disposal,
                occurredAt = DateTimeOffset.UtcNow,
                recordedBy = "capataz",
                payloadJson = $"{{\"count\":{count},\"causeId\":null}}",
                affectedCount = count,
            });
            response.EnsureSuccessStatusCode();
        }

        var countResponse = await _client.GetAsync($"/api/v1/animal-groups/{groupId}/live-head-count");
        countResponse.EnsureSuccessStatusCode();
        var body = await countResponse.Content.ReadFromJsonAsync<LiveHeadCountDto>();
        Assert.Equal(0, body!.LiveHeadCount);

        var groupResponse = await _client.GetAsync($"/api/v1/animal-groups/{groupId}");
        groupResponse.EnsureSuccessStatusCode();
        var group = await groupResponse.Content.ReadFromJsonAsync<AnimalGroupDto>();
        Assert.All(group!.Memberships, m => Assert.False(m.IsActive));
    }

    /// <summary>
    /// An individually tracked animal moved into a headcount lot has no individual answer
    /// (ADR-0015 sec.6) — until the lot's cascade closure actually disposes it.
    /// </summary>
    [Fact]
    public async Task IndividualState_ForAnimalInHeadcountGroup_IsIndeterminate_UntilCascadeCloses()
    {
        var (groupId, animalIds) = await SeedHeadcountGroupAsync(2);
        var animalId = animalIds[0];

        var beforeResponse = await _client.GetAsync($"/api/v1/animals/{animalId}/individual-state");
        beforeResponse.EnsureSuccessStatusCode();
        var before = await beforeResponse.Content.ReadFromJsonAsync<IndividualStateDto>();
        Assert.Equal(nameof(IndividualState.Indeterminate), before!.State);

        var response = await _client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/events", new
        {
            eventType = EventType.Disposal,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "capataz",
            payloadJson = "{\"count\":2,\"causeId\":null}",
            affectedCount = 2,
        });
        response.EnsureSuccessStatusCode();

        var afterResponse = await _client.GetAsync($"/api/v1/animals/{animalId}/individual-state");
        afterResponse.EnsureSuccessStatusCode();
        var after = await afterResponse.Content.ReadFromJsonAsync<IndividualStateDto>();
        Assert.Equal(nameof(IndividualState.Disposed), after!.State);
    }

    [Fact]
    public async Task IndividualState_ForAnimalNeverInAGroup_IsAlive()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);

        var response = await _client.GetAsync($"/api/v1/animals/{animalId}/individual-state");
        response.EnsureSuccessStatusCode();
        var state = await response.Content.ReadFromJsonAsync<IndividualStateDto>();

        Assert.Equal(nameof(IndividualState.Alive), state!.State);
    }

    /// <summary>
    /// The CHECK constraint is the backstop the domain factories cannot bypass (ADR-0015
    /// sec.2): any write that reaches Postgres with both, or neither, subject columns set
    /// fails loudly at the database, which is what protects a consumer that still assumes
    /// AnimalId is never null. The original test only covered the two rejected cases;
    /// the two accepted cases (animal-only, group-only) were never pinned, which is the
    /// half of the contract most likely to regress silently if the CHECK is ever
    /// weakened in a future migration.
    /// </summary>
    [Fact]
    public async Task DatabaseCheckConstraint_RejectsRowsWithBothOrNeitherSubject()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LivestockDbContext>();

        // Note on the payload_json literal: the string is interpolated as raw SQL,
        // not as a parameterised placeholder, so the value `'{"x":1}'` (a JSON object
        // with one property) is sent verbatim to Postgres. The original test used
        // `'{}'` which the SQL formatter pre-processor used to misread as a malformed
        // `{}` placeholder before the call reached the server — the assertion below
        // would then have caught a FormatException instead of a CHECK violation, and
        // the CHECK would have been left unverified.
        var neitherEx = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO livestock.animal_events
                (id, animal_id, group_id, event_type, occurred_at, recorded_by, payload_json, created_at)
            VALUES
                (gen_random_uuid(), NULL, NULL, 'Weighing', now(), 'test', '{"x":1}', now())
            """));
        Assert.Equal("23514", neitherEx.SqlState);

        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);
        var (groupId, _) = await SeedHeadcountGroupAsync(1);

        var bothInsertSql =
            "INSERT INTO livestock.animal_events " +
            "(id, animal_id, group_id, event_type, occurred_at, recorded_by, payload_json, created_at) " +
            "VALUES " +
            $"(gen_random_uuid(), '{animalId}', '{groupId}', 'Weighing', now(), 'test', '{{\"x\":1}}', now())";

        var bothEx = await Assert.ThrowsAsync<Npgsql.PostgresException>(
            () => dbContext.Database.ExecuteSqlRawAsync(bothInsertSql));
        Assert.Equal("23514", bothEx.SqlState);
    }

    /// <summary>
    /// Companion to <see cref="DatabaseCheckConstraint_RejectsRowsWithBothOrNeitherSubject"/>:
    /// the accepted half of the XOR. A row with <c>animal_id</c> set and <c>group_id</c>
    /// null is the normal case (animal-subject event) and must survive the CHECK.
    /// </summary>
    [Fact]
    public async Task DatabaseCheckConstraint_AcceptsRowWithOnlyAnimalSubject()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LivestockDbContext>();

        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);

        var sql =
            "INSERT INTO livestock.animal_events " +
            "(id, animal_id, group_id, event_type, occurred_at, recorded_by, payload_json, created_at) " +
            "VALUES " +
            $"(gen_random_uuid(), '{animalId}', NULL, 'Weighing', now(), 'test', '{{\"x\":1}}', now())";

        await dbContext.Database.ExecuteSqlRawAsync(sql);
    }

    /// <summary>
    /// Companion to <see cref="DatabaseCheckConstraint_RejectsRowsWithBothOrNeitherSubject"/>:
    /// the second accepted half of the XOR. A row with <c>group_id</c> set and
    /// <c>animal_id</c> null is the group-subject event (ADR-0015) and must survive
    /// the CHECK.
    /// </summary>
    [Fact]
    public async Task DatabaseCheckConstraint_AcceptsRowWithOnlyGroupSubject()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LivestockDbContext>();

        var (_, groupId) = await SeedHeadcountGroupAsync(1);

        var sql =
            "INSERT INTO livestock.animal_events " +
            "(id, animal_id, group_id, event_type, occurred_at, recorded_by, payload_json, created_at) " +
            "VALUES " +
            $"(gen_random_uuid(), NULL, '{groupId}', 'Weighing', now(), 'test', '{{\"x\":1}}', now())";

        await dbContext.Database.ExecuteSqlRawAsync(sql);
    }

    /// <summary>
    /// BLOQUE C / C3: the group disposal handler must validate CauseId the same
    /// way the individual handler does. Before the fix, the group handler
    /// skipped the check and would either silently store a dangling FK or, when
    /// the FK is present, throw an opaque DbUpdateException instead of the
    /// domain-level rejection the rest of the codebase uses. The asymmetry let
    /// an operator file a group disposal with a cause the catalog had already
    /// retired — the same paperwork routine that was blocked for individual
    /// disposals (see <see cref="MortalityCauseApiTests.RecordIndividualDisposal_WithInactiveCause_IsRejected"/>).
    /// </summary>
    [Fact]
    public async Task RecordGroupDisposal_WithInactiveCause_IsRejected()
    {
        var (groupId, _) = await SeedHeadcountGroupAsync(3);

        // Create a cause, then retire it via DELETE (the catalog's soft-delete).
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/mortality-causes",
            new CreateMortalityCauseRequest($"Causa-{Guid.NewGuid():N}"));
        createResponse.EnsureSuccessStatusCode();
        var causeId = (await createResponse.Content.ReadFromJsonAsync<CreatedId>())!.Id;
        (await _client.DeleteAsync($"/api/v1/mortality-causes/{causeId}")).EnsureSuccessStatusCode();

        // Act: try to file a group disposal with the now-inactive cause.
        var response = await _client.PostAsJsonAsync($"/api/v1/animal-groups/{groupId}/events", new
        {
            eventType = EventType.Disposal,
            occurredAt = DateTimeOffset.UtcNow,
            recordedBy = "capataz",
            payloadJson = "{\"count\":1,\"causeId\":null}",
            affectedCount = 1,
            causeId,
        });

        // Assert: the same domain-level rejection the individual handler gives.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Side-effect guard: a rejected request must not have lowered the live
        // head count. This catches the failure mode where the cause validation
        // happens after the disposal has already mutated the group.
        var countResponse = await _client.GetAsync($"/api/v1/animal-groups/{groupId}/live-head-count");
        countResponse.EnsureSuccessStatusCode();
        var body = await countResponse.Content.ReadFromJsonAsync<LiveHeadCountDto>();
        Assert.Equal(3, body!.LiveHeadCount);
    }

    private sealed record CreatedId(Guid Id);
    private sealed record LiveHeadCountDto(int LiveHeadCount);
    private sealed record IndividualStateDto(string State);
}
