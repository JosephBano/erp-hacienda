using MediatR;

namespace Hato.SharedKernel;

/// <summary>
/// Domain event published in-process via MediatR. This is the only way modules
/// react to each other (Art. 6).
/// </summary>
public interface IDomainEvent : INotification
{
    DateTimeOffset OccurredAt { get; }
}
