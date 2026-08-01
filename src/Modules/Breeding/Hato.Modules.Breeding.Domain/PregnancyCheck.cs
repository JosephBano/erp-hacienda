using Hato.SharedKernel;
using Hato.Modules.Breeding.Domain.Enums;
using Hato.Modules.Breeding.Domain.Events;

namespace Hato.Modules.Breeding.Domain;

public class PregnancyCheck : AuditableEntity
{
    public Guid ServiceId { get; private set; }
    public Guid DamId { get; private set; }
    public DateOnly CheckDate { get; private set; }
    public CheckMethod Method { get; private set; }
    public CheckResult Result { get; private set; }
    public string? CheckedBy { get; private set; }
    public string? Notes { get; private set; }

    private PregnancyCheck() { } // EF Core

    public static PregnancyCheck Create(
        Guid serviceId,
        Guid damId,
        DateOnly checkDate,
        CheckMethod method,
        CheckResult result,
        string? checkedBy = null,
        string? notes = null)
    {
        if (serviceId == Guid.Empty)
            throw new DomainException("Service ID is required for a pregnancy check.");
        if (damId == Guid.Empty)
            throw new DomainException("Dam ID is required for a pregnancy check.");

        var check = new PregnancyCheck
        {
            Id = Guid.NewGuid(),
            ServiceId = serviceId,
            DamId = damId,
            CheckDate = checkDate,
            Method = method,
            Result = result,
            CheckedBy = checkedBy?.Trim(),
            Notes = notes?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        check.RaiseDomainEvent(new PregnancyCheckRecordedEvent(
            check.Id,
            check.ServiceId,
            check.DamId,
            check.Result,
            check.CheckDate
        ));

        return check;
    }
}
