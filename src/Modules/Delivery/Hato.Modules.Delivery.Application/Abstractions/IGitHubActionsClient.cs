using Hato.Modules.Delivery.Domain;

namespace Hato.Modules.Delivery.Application.Abstractions;

public record GitHubWorkflowRunDto(
    long Id,
    int RunAttempt,
    string WorkflowName,
    string HeadSha,
    string Status,
    string? Conclusion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public interface IGitHubActionsClient
{
    Task<bool> DispatchWorkflowAsync(MobileBuildRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitHubWorkflowRunDto>> ListWorkflowRunsAsync(string workflowFileName, CancellationToken cancellationToken = default);
    Task<Stream?> DownloadArtifactAsync(long runId, string artifactName, CancellationToken cancellationToken = default);
}
