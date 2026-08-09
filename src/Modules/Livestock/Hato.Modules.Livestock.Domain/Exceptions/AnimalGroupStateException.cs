namespace Hato.Modules.Livestock.Domain.Exceptions;

/// <summary>
/// Thrown when a request is well-formed but the current state of an
/// <see cref="AnimalGroup"/> forbids the operation. Maps to HTTP 409
/// (Conflict) via <c>ApiExceptionHandler</c> — distinct from
/// <see cref="SharedKernel.DomainException"/>, which maps to 400
/// (Bad Request) and signals a business rule violation by the caller.
/// </summary>
/// <remarks>
/// Concrete case in this codebase: changing <see cref="TrackingMode"/>
/// once the group has active memberships or recorded events would
/// either rewrite the past (Art. 1) or fabricate identity within an
/// anonymous lot (ADR-0015 sec.7). The handler raises this exception
/// in those cases so the API surfaces a 409, not a 400.
/// </remarks>
public class AnimalGroupStateException(string message) : Exception(message);
