using System.Net.Http.Json;
using System.Text.Json;
using Hato.Modules.Livestock.Application.Events;
using Hato.Modules.Livestock.Domain;
using Hato.Modules.Livestock.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// BLOQUE C / C1: a correction reason with newlines, tabs, quotes, backslashes,
/// or a U+2028 line separator must round-trip into the jsonb column without
/// losing data or breaking the JSON parser. The Fase 3.5 fix replaced the
/// hand-rolled Escape that only handled \\ and " with System.Text.Json's
/// serializer, which gets Unicode control characters right out of the box.
///
/// Goes through ISender directly because there is no HTTP endpoint for
/// correction (the field app reaches the handler through the push batch).
/// </summary>
public class RecordCorrectionCommandApiTests(HatoApiFactory factory)
    : IClassFixture<HatoApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RecordCorrection_WithReasonContainingNewlinesAndTabs_PersistsValidJsonb()
    {
        var speciesId = await CreateSpeciesAsync();
        var animalId = await CreateAnimalAsync(speciesId);

        Guid originalEventId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LivestockDbContext>();
            var original = AnimalEvent.Create(
                animalId, EventType.Weighing, DateTimeOffset.UtcNow,
                recordedBy: "capataz", payloadJson: "{\"weightKg\":180}");
            db.AnimalEvents.Add(original);
            await db.SaveChangesAsync();
            originalEventId = original.Id;
        }

        // The exact reason text that broke the original hand-rolled escape:
        // newline, tab, double quote, backslash, and a U+2028 line separator.
        var reason = "registré mal el pesaje\nlínea 2 con\ttabulación\n" +
                     "y comillas: \"210 kg\" y backslash \\ final " +
                     "separador U+2028 \u2028 al medio";

        Guid correctionId;
        using (var actScope = factory.Services.CreateScope())
        {
            var sender = actScope.ServiceProvider.GetRequiredService<ISender>();
            correctionId = await sender.Send(new RecordCorrectionCommand(
                originalEventId, DateTimeOffset.UtcNow, "capataz", reason));
        }

        using var assertScope = factory.Services.CreateScope();
        var dbContext = assertScope.ServiceProvider.GetRequiredService<LivestockDbContext>();
        var persisted = await dbContext.AnimalEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == correctionId);

        Assert.NotNull(persisted);
        Assert.Equal(EventType.Correction, persisted!.EventType);
        Assert.Equal(originalEventId, persisted.RelatedEventId);

        using var doc = JsonDocument.Parse(persisted.PayloadJson);
        var roundTripped = doc.RootElement.GetProperty("reason").GetString();
        Assert.Equal(reason, roundTripped);
    }

    private async Task<Guid> CreateSpeciesAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/species", new { name = $"Porcino-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedId>())!.Id;
    }

    private async Task<Guid> CreateAnimalAsync(Guid speciesId)
    {
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

    private sealed record CreatedId(Guid Id);
}