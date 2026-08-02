namespace Hato.SharedKernel;

/// <summary>Thrown when a domain invariant is violated. Never used for infrastructure failures.</summary>
public class DomainException(string message) : Exception(message);
