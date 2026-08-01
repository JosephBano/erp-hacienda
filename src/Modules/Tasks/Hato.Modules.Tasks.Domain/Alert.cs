using Hato.SharedKernel;
using Hato.Modules.Tasks.Domain.Enums;

namespace Hato.Modules.Tasks.Domain;

public class Alert : AuditableEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public AlertSeverity Severity { get; private set; }
    public Guid? TargetEntityId { get; private set; }
    public bool IsDismissed { get; private set; }
    public DateTimeOffset? DismissedAt { get; private set; }
    public Guid? DismissedBy { get; private set; }

    private Alert() { } // EF Core

    public static Alert Create(
        string code,
        string title,
        string message,
        AlertSeverity severity,
        Guid? targetEntityId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Alert code is required.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Alert title is required.");
        if (string.IsNullOrWhiteSpace(message))
            throw new DomainException("Alert message is required.");

        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            Code = code.Trim(),
            Title = title.Trim(),
            Message = message.Trim(),
            Severity = severity,
            TargetEntityId = targetEntityId,
            IsDismissed = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        return alert;
    }

    public void Dismiss(Guid? userId = null)
    {
        if (IsDismissed)
            return;

        IsDismissed = true;
        DismissedAt = DateTimeOffset.UtcNow;
        DismissedBy = userId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
