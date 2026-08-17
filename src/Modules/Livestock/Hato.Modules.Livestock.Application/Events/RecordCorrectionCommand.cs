using System.Text.Json;
using FluentValidation;
using Hato.Modules.Livestock.Application.Abstractions;
using Hato.Modules.Livestock.Domain;
using Hato.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hato.Modules.Livestock.Application.Events;

/// <summary>
/// Server side of the field-app correction flow (docs/spec/plan-0002-fase-3-5/spec-3.5a.md sec.3.5a.8,
/// ADR-0017). The original event is never edited (Art. 1): a correction is a new event
/// referencing the original via <see cref="AnimalEvent.RelatedEventId"/>. The payload
/// is the operator's stated reason; the original payload is preserved untouched.
///
/// The window is "same calendar day" for the field operator (ADMIN-FROM-PANEL has no
/// window, per the design discussion): passing that boundary is rejected so the
/// weekend-night case "el lunes corrijo lo del viernes" cannot accidentally rewrite
/// history. The boundary is checked in UTC because that is the storage time zone
/// (Art. 6 + AGENTS.md rule 6); the operator sees local time on the phone, the server
/// sees the UTC tick that came through the push, and the two are equivalent at the
/// midnight boundary for any place within +/-14h of Greenwich, which covers Ecuador.
/// </summary>
public record RecordCorrectionCommand(
    Guid OriginalEventId,
    DateTimeOffset OccurredAt,
    string RecordedBy,
    string Reason,
    Guid? RecordedById = null) : IRequest<Guid>;

public class RecordCorrectionCommandValidator : AbstractValidator<RecordCorrectionCommand>
{
    public RecordCorrectionCommandValidator()
    {
        RuleFor(x => x.OriginalEventId).NotEmpty();
        RuleFor(x => x.RecordedBy).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class RecordCorrectionCommandHandler(ILivestockDbContext dbContext)
    : IRequestHandler<RecordCorrectionCommand, Guid>
{
    /// <summary>Maximum age of the original event for a field correction. The admin
    /// panel does not enforce this; the panel is the canonical way to fix old records.
    /// </summary>
    public static readonly TimeSpan FieldCorrectionWindow = TimeSpan.FromHours(24);

    public async Task<Guid> Handle(RecordCorrectionCommand request, CancellationToken cancellationToken)
    {
        var original = await dbContext.AnimalEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.OriginalEventId, cancellationToken)
            ?? throw new DomainException(
                $"El evento original '{request.OriginalEventId}' no existe; no se puede corregir.");

        // Window check: the correction must arrive within the same UTC calendar day as
        // the original. The admin panel bypasses this entirely (different command
        // path); only the field flow is bounded by the day, because that is the
        // window where "I typed it wrong five minutes ago" is the realistic error.
        var occurredAtUtc = request.OccurredAt.ToUniversalTime();
        var originalDay = original.OccurredAt.UtcDateTime.Date;
        var correctionDay = occurredAtUtc.UtcDateTime.Date;
        if (originalDay != correctionDay)
        {
            throw new DomainException(
                "La corrección de campo sólo se permite dentro del mismo día calendario " +
                "del registro original. Para correcciones más antiguas use el panel.");
        }

        // The reason is stored as JSONB so the field-app can grow the schema later
        // (a free-text correction reason is a starting point, not a final contract).
        //
        // Bug from the Fase 3.5 retrospective: this used to be a hand-rolled string
        // template with a tiny `Escape` that only handled `\\` and `"`. Any reason
        // with a newline, a tab, a control character, or a U+2028 line separator
        // produced invalid JSON against the jsonb column and the entire correction
        // was rejected — exactly the operator who says "lo escribí mal en lunes,
        // corrijo el martes" cannot correct. System.Text.Json escapes Unicode
        // control characters correctly out of the box.
        var payloadJson = JsonSerializer.Serialize(new { reason = request.Reason });

        // The Correction event's subject mirrors the original (Art. 1: the
        // correction is about the same animal/group). AnimalEvent.Create and
        // CreateForGroup exist for the same reason the original was created that
        // way — the XOR on AnimalId / GroupId is enforced by the domain factory
        // and by the DB CHECK constraint.
        AnimalEvent correction;
        if (original.AnimalId.HasValue)
        {
            correction = AnimalEvent.Create(
                original.AnimalId.Value,
                EventType.Correction,
                occurredAtUtc,
                request.RecordedBy,
                payloadJson,
                cost: null,
                relatedEventId: original.Id,
                recordedById: request.RecordedById);
        }
        else
        {
            correction = AnimalEvent.CreateForGroup(
                original.GroupId ?? Guid.Empty,
                EventType.Correction,
                occurredAtUtc,
                request.RecordedBy,
                payloadJson,
                affectedCount: original.AffectedCount,
                cost: null,
                relatedEventId: original.Id,
                recordedById: request.RecordedById);
        }

        dbContext.AnimalEvents.Add(correction);

        await dbContext.SaveChangesAsync(cancellationToken);

        return correction.Id;
    }
}
