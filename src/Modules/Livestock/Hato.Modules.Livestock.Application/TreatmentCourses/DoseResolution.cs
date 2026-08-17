using System.Text.Json;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Application.AnimalGroups;
using Hato.Modules.Livestock.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.TreatmentCourses;

/// <summary>
/// Thrown when <see cref="DoseKind.Keys.PerWeight"/> is requested for a subject with
/// no weighing to resolve against (task 6). Distinct from
/// <see cref="Hato.SharedKernel.DomainException"/> so <c>ApiExceptionHandler</c> can
/// attach a typed error code the field-app can branch on ("switch to Absolute, or
/// weigh first") instead of parsing a Spanish sentence.
/// </summary>
public class MissingWeighingForDoseException(Guid subjectId, bool isGroup) : Exception(
    isGroup
        ? $"El lote '{subjectId}' no tiene un pesaje de muestra registrado. No se puede calcular una dosis por peso."
        : $"El animal '{subjectId}' no tiene un pesaje registrado. No se puede calcular una dosis por peso.")
{
    public const string ErrorCode = "dose_missing_weighing";

    public Guid SubjectId { get; } = subjectId;
    public bool IsGroup { get; } = isGroup;
}

/// <summary>Result of resolving <see cref="DoseKind"/> + factor against a subject
/// (task 2 + task 6). <see cref="AutoNote"/> is non-null only for the group/estimated
/// path, citing the sample size the average came from.</summary>
public record ResolvedDose(decimal Amount, string Unit, bool IsEstimated, string? AutoNote);

/// <summary>
/// Resolves <see cref="TreatmentCourse.DoseFactorAmount"/> against a subject's last
/// weighing (task 6 of docs/planes/fase-3-5/sub-planes/3.5a.2-B.md). One place, so
/// <see cref="CreateTreatmentCourseCommand"/> and
/// <see cref="AddTreatmentCourseApplicationCommand"/> cannot drift apart on the
/// formula.
/// </summary>
public static class DoseResolver
{
    public static async Task<ResolvedDose> ResolveAsync(
        ILivestockDbContext dbContext,
        string doseKindKey,
        decimal factorAmount,
        string factorUnit,
        Guid? animalId,
        Guid? groupId,
        CancellationToken cancellationToken)
    {
        switch (doseKindKey)
        {
            case DoseKind.Keys.Absolute:
            case DoseKind.Keys.PerHead:
                // Neither form depends on a weighing: Absolute is a fixed quantity,
                // PerHead is a flat dose repeated per head.
                return new ResolvedDose(factorAmount, factorUnit, IsEstimated: false, AutoNote: null);

            case DoseKind.Keys.PerWeight when animalId is { } animal:
                {
                    var lastWeightKg = await GetLastAnimalWeighingKgAsync(dbContext, animal, cancellationToken);
                    if (lastWeightKg is null)
                        throw new MissingWeighingForDoseException(animal, isGroup: false);

                    return new ResolvedDose(
                        Math.Round(factorAmount * lastWeightKg.Value, 3, MidpointRounding.AwayFromZero),
                        factorUnit,
                        IsEstimated: false,
                        AutoNote: null);
                }

            case DoseKind.Keys.PerWeight when groupId is { } group:
                {
                    var sample = await GetLastGroupWeighingAsync(dbContext, group, cancellationToken);
                    if (sample is null)
                        throw new MissingWeighingForDoseException(group, isGroup: true);

                    var liveHeadCount = await ComputeLiveHeadCountAsync(dbContext, group, cancellationToken);
                    var amount = Math.Round(
                        factorAmount * sample.Value.AvgKg * liveHeadCount, 3, MidpointRounding.AwayFromZero);

                    return new ResolvedDose(
                        amount,
                        factorUnit,
                        IsEstimated: true,
                        AutoNote: $"Calculado sobre muestreo de {sample.Value.SampleCount} cabezas.");
                }

            default:
                throw new Hato.SharedKernel.DomainException(
                    $"La forma de dosis '{doseKindKey}' no existe en el catálogo o el sujeto no es válido para ella.");
        }
    }

    /// <summary>Last individual weighing (<see cref="EventType.Weighing"/>) for an
    /// animal. The field-app writes it as <c>{"weightKg": ...}</c> — see
    /// <c>clients/field-app/src/services/eventService.ts</c>.</summary>
    private static async Task<decimal?> GetLastAnimalWeighingKgAsync(
        ILivestockDbContext dbContext, Guid animalId, CancellationToken cancellationToken)
    {
        var payload = await dbContext.AnimalEvents
            .AsNoTracking()
            .Where(e => e.AnimalId == animalId && e.EventType == EventType.Weighing)
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => e.PayloadJson)
            .FirstOrDefaultAsync(cancellationToken);

        if (payload is null) return null;

        using var document = JsonDocument.Parse(payload);
        return TryGetDecimal(document.RootElement, "weightKg");
    }

    /// <summary>Last group sample weighing (<see cref="EventType.Weighing"/> on a
    /// group subject). Written as
    /// <c>{"sampleCount": ..., "avgKg": ..., "minKg": ..., "maxKg": ...}</c>.</summary>
    private static async Task<(int SampleCount, decimal AvgKg)?> GetLastGroupWeighingAsync(
        ILivestockDbContext dbContext, Guid groupId, CancellationToken cancellationToken)
    {
        var payload = await dbContext.AnimalEvents
            .AsNoTracking()
            .Where(e => e.GroupId == groupId && e.EventType == EventType.Weighing)
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => e.PayloadJson)
            .FirstOrDefaultAsync(cancellationToken);

        if (payload is null) return null;

        using var document = JsonDocument.Parse(payload);
        var sampleCount = TryGetDecimal(document.RootElement, "sampleCount");
        var avgKg = TryGetDecimal(document.RootElement, "avgKg");

        if (sampleCount is null || avgKg is null) return null;

        return ((int)sampleCount.Value, avgKg.Value);
    }

    private static async Task<int> ComputeLiveHeadCountAsync(
        ILivestockDbContext dbContext, Guid groupId, CancellationToken cancellationToken)
    {
        var activeMemberships = await dbContext.GroupMemberships
            .Where(m => m.GroupId == groupId && m.LeftAt == null)
            .CountAsync(cancellationToken);

        return await GetAnimalGroupByIdHandler.ComputeLiveHeadCountAsync(
            dbContext, groupId, activeMemberships, cancellationToken);
    }

    private static decimal? TryGetDecimal(JsonElement root, string propertyName)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            return property.Value.ValueKind switch
            {
                JsonValueKind.Number => property.Value.GetDecimal(),
                JsonValueKind.String when decimal.TryParse(property.Value.GetString(), out var parsed) => parsed,
                _ => null,
            };
        }

        return null;
    }
}
