namespace Hato.Modules.Tasks.Contracts;

public record AlertDto(
    Guid Id,
    string Code,
    string Title,
    string Message,
    string Severity,
    Guid? TargetEntityId,
    bool IsDismissed,
    DateTimeOffset CreatedAt
);
