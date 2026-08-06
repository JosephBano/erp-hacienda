namespace Hato.SharedKernel;

/// <summary>
/// Abstraction for the currently authenticated user in the system (Art. 4 + LOPDP Audit).
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? UserEmail { get; }
    string? FullName { get; }
    bool IsAuthenticated { get; }
}
