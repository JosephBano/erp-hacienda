namespace Hato.SharedKernel;

/// <summary>
/// Universal audit + logical deletion (Art. 1: data is never physically destroyed).
/// Timestamps are stored in UTC; America/Guayaquil is presentation-only.
/// </summary>
public abstract class AuditableEntity : Entity
{
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public bool IsDeleted => DeletedAt is not null;
}
