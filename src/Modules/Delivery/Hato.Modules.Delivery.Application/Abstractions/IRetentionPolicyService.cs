namespace Hato.Modules.Delivery.Application.Abstractions;

public record RetentionExecutionResult(
    string Channel,
    int EvaluatedReleases,
    int PrunedReleases,
    int ProtectedReleases,
    long BytesFreed,
    IReadOnlyList<Guid> PrunedReleaseIds);

public interface IRetentionPolicyService
{
    Task<RetentionExecutionResult> ApplyRetentionAsync(
        string channel,
        bool dryRun = false,
        CancellationToken cancellationToken = default);
}
