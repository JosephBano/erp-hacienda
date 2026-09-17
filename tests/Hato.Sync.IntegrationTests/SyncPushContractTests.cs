using System.Text.Json;
using System.Text.RegularExpressions;
using Hato.Api.Sync;
using Hato.Modules.Breeding.Application.Birthings;
using Hato.Modules.Livestock.Application.Animals;
using Hato.Modules.Livestock.Application.Events;
using Hato.Modules.Livestock.Application.TreatmentCourses;
using Hato.Modules.Production.Application.Milking;
using Xunit;

namespace Hato.Sync.IntegrationTests;

public class SyncPushContractTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "contracts", "push-payloads.json")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repository root containing docs/contracts/push-payloads.json");
    }

    private static JsonElement GetFixturePayload(string operationType)
    {
        var repoRoot = FindRepoRoot();
        var fixtureFile = Path.Combine(repoRoot, "docs", "contracts", "push-payloads.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(fixtureFile));

        foreach (var op in doc.RootElement.GetProperty("operations").EnumerateArray())
        {
            if (string.Equals(op.GetProperty("operationType").GetString(), operationType, StringComparison.OrdinalIgnoreCase))
            {
                return op.GetProperty("payload").Clone();
            }
        }

        throw new InvalidOperationException($"Operation '{operationType}' not found in fixture");
    }

    [Fact]
    public void PushContracts_Completeness_BothDirections()
    {
        var repoRoot = FindRepoRoot();
        var pushCommandsFile = Path.Combine(repoRoot, "src", "Hato.Api", "Sync", "PushSyncCommands.cs");
        var pushCommandsCode = File.ReadAllText(pushCommandsFile);

        var caseMatches = Regex.Matches(pushCommandsCode, @"case\s+""([^""]+)""\s*:");
        var dispatcherCases = caseMatches.Select(m => m.Groups[1].Value.ToLowerInvariant()).ToHashSet();

        var fixtureFile = Path.Combine(repoRoot, "docs", "contracts", "push-payloads.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(fixtureFile));
        var fixtureTypes = doc.RootElement.GetProperty("operations")
            .EnumerateArray()
            .Select(op => op.GetProperty("operationType").GetString()!.ToLowerInvariant())
            .ToHashSet();

        Assert.Equal(dispatcherCases, fixtureTypes);
    }

    [Theory]
    [InlineData("createAnimal")]
    [InlineData("recordBirth")]
    [InlineData("recordAnimalEvent")]
    [InlineData("createTreatmentCourse")]
    [InlineData("recordGroupEvent")]
    [InlineData("recordFeedConsumption")]
    [InlineData("moveAnimal")]
    [InlineData("updateAnimal")]
    [InlineData("recordCorrection")]
    [InlineData("recordMilking")]
    [InlineData("assignAnimalIdentifier")]
    public void PushContracts_PayloadDeserializesWithoutUnmappedMembers(string operationType)
    {
        var payloadElement = GetFixturePayload(operationType);
        var rawText = payloadElement.GetRawText();

        switch (operationType.ToLowerInvariant())
        {
            case "recordmilking":
                var milking = JsonSerializer.Deserialize<RecordMilkingSessionCommand>(rawText, PushSyncBatchCommandHandler.JsonOptions);
                Assert.NotNull(milking);
                break;

            case "recordanimalevent":
                var animalEvent = JsonSerializer.Deserialize<RecordAnimalEventCommand>(rawText, PushSyncBatchCommandHandler.JsonOptions);
                Assert.NotNull(animalEvent);
                break;

            case "createtreatmentcourse":
                var treatmentCourse = JsonSerializer.Deserialize<CreateTreatmentCourseCommand>(rawText, PushSyncBatchCommandHandler.JsonOptions);
                Assert.NotNull(treatmentCourse);
                break;

            case "recordgroupevent":
                var groupEvent = JsonSerializer.Deserialize<RecordGroupEventCommand>(rawText, PushSyncBatchCommandHandler.JsonOptions);
                Assert.NotNull(groupEvent);
                break;

            case "recordfeedconsumption":
                var feed = JsonSerializer.Deserialize<RecordFeedConsumptionPushPayload>(rawText, PushSyncBatchCommandHandler.JsonOptions);
                Assert.NotNull(feed);
                break;

            case "createanimal":
                var animal = JsonSerializer.Deserialize<RegisterAnimalCommand>(rawText, PushSyncBatchCommandHandler.JsonOptions);
                Assert.NotNull(animal);
                break;

            case "recordbirth":
                var birth = JsonSerializer.Deserialize<RecordBirthingCommand>(rawText, PushSyncBatchCommandHandler.JsonOptions);
                Assert.NotNull(birth);
                break;

            case "moveanimal":
                var move = JsonSerializer.Deserialize<MoveAnimalPayload>(rawText, PushSyncBatchCommandHandler.JsonOptions);
                Assert.NotNull(move);
                break;

            case "updateanimal":
                var update = JsonSerializer.Deserialize<UpdateAnimalPushPayload>(rawText, PushSyncBatchCommandHandler.JsonOptions);
                Assert.NotNull(update);
                break;

            case "recordcorrection":
                var correction = JsonSerializer.Deserialize<RecordCorrectionPushPayload>(rawText, PushSyncBatchCommandHandler.JsonOptions);
                Assert.NotNull(correction);
                break;

            case "assignanimalidentifier":
                var assign = JsonSerializer.Deserialize<AssignAnimalIdentifierPushPayload>(rawText, PushSyncBatchCommandHandler.JsonOptions);
                Assert.NotNull(assign);
                break;

            default:
                throw new InvalidOperationException($"Tipo de operación no reconocido en el arnés: {operationType}");
        }
    }
}
