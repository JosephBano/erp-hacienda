namespace Hato.Modules.Delivery.Application.Abstractions;

public interface IVersionCodeReservator
{
    /// <summary>
    /// Monotonically reserves the next versionCode for a specific Android package inside a transaction.
    /// </summary>
    Task<int> ReserveNextVersionCodeAsync(string packageName, CancellationToken cancellationToken = default);
}
