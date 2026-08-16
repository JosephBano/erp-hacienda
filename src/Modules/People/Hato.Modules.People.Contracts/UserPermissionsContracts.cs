namespace Hato.Modules.People.Contracts;

/// <summary>
/// Public read port other modules use to ask "what can this user see", without depending
/// on People.Domain (Art. 6). The sync pull is the first consumer: docs/planes/fase-3/spec.md sec.3.A
/// requires that a device only downloads the collections its user's role can read.
/// </summary>
public interface IUserPermissionsReader
{
    /// <summary>
    /// Every permission code effectively granted to the user through their active roles.
    /// Admin's role already carries every code in the RBAC seed, so no special-case
    /// bypass is needed here — the set itself already contains everything.
    /// </summary>
    Task<HashSet<string>> GetPermissionCodesAsync(Guid userId, CancellationToken cancellationToken);
}
