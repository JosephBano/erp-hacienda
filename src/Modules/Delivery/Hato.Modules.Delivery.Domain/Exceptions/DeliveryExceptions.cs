using Hato.SharedKernel;

namespace Hato.Modules.Delivery.Domain.Exceptions;

public class DeliveryDomainException(string message) : DomainException(message);

public class InvalidReleaseTransitionException(string message) : DeliveryDomainException(message);

public class DuplicateBuildRequestException(string message) : DeliveryDomainException(message);

public class ArtifactIntegrityException(string message) : DeliveryDomainException(message);

public class DeliverySecurityException(string message) : DeliveryDomainException(message);

public class StorageQuotaExceededException(string message) : DeliveryDomainException(message);

public class BuildQuotaExceededException(string message) : DeliveryDomainException(message);
