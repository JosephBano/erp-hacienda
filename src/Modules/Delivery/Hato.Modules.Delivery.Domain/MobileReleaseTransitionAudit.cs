using Hato.SharedKernel;

namespace Hato.Modules.Delivery.Domain;

public class MobileReleaseTransitionAudit : Entity
{
    public Guid ReleaseId { get; private set; }
    public string FromStatus { get; private set; } = null!;
    public string ToStatus { get; private set; } = null!;
    public DateTimeOffset ChangedAt { get; private set; }
    public Guid? ChangedBy { get; private set; }
    public string? Reason { get; private set; }

    private MobileReleaseTransitionAudit() { }

    public static MobileReleaseTransitionAudit Create(
        Guid releaseId,
        string fromStatus,
        string toStatus,
        Guid? changedBy,
        string? reason)
    {
        return new MobileReleaseTransitionAudit
        {
            Id = Guid.NewGuid(),
            ReleaseId = releaseId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ChangedAt = DateTimeOffset.UtcNow,
            ChangedBy = changedBy,
            Reason = reason?.Trim(),
        };
    }
}
