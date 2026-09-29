using Hato.SharedKernel;

namespace Hato.Modules.Delivery.Domain.Exceptions;

public class DeliveryDomainException(string message) : DomainException(message);

public class InvalidReleaseTransitionException(string message) : DeliveryDomainException(message);

public class DuplicateBuildRequestException(string message) : DeliveryDomainException(message);
