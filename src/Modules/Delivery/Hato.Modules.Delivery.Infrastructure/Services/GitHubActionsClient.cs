using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hato.Modules.Delivery.Application.Abstractions;
using Hato.Modules.Delivery.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hato.Modules.Delivery.Infrastructure.Services;

public class GitHubActionsClient : IGitHubActionsClient
{
    private readonly HttpClient _httpClient;
    private readonly string _repository;
    private readonly string _token;
    private readonly ILogger<GitHubActionsClient> _logger;

    public GitHubActionsClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GitHubActionsClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _repository = configuration["Delivery:GitHubRepository"] ?? "joemandev/erp-hato";
        _token = configuration["Delivery:GitHubToken"] ?? string.Empty;

        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri("https://api.github.com/");
        }

        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Hato-Delivery", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        if (!string.IsNullOrWhiteSpace(_token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }
    }

    public async Task<bool> DispatchWorkflowAsync(MobileBuildRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            _logger.LogWarning("Delivery:GitHubToken no configurado. Simulación de dispatch para solicitud {RequestId}.", request.Id);
            return true;
        }

        var endpoint = $"repos/{_repository}/actions/workflows/build-android.yml/dispatches";

        var payload = new
        {
            @ref = request.CommitSha,
            inputs = new Dictionary<string, string>
            {
                ["channel"] = request.Channel,
                ["version_code"] = request.VersionCode.ToString(),
                ["build_request_id"] = request.Id.ToString(),
                ["commit_sha"] = request.CommitSha,
                ["release_tag"] = request.ReleaseTag ?? string.Empty
            }
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Enviando dispatch a GitHub Actions: {Endpoint} (RequestId: {Id})", endpoint, request.Id);
        var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Fallo en dispatch a GitHub Actions ({StatusCode}): {Error}", response.StatusCode, err);
            return false;
        }

        return true;
    }

    public async Task<IReadOnlyList<GitHubWorkflowRunDto>> ListWorkflowRunsAsync(
        string workflowFileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            return [];
        }

        var endpoint = $"repos/{_repository}/actions/workflows/{workflowFileName}/runs?per_page=30";
        var response = await _httpClient.GetAsync(endpoint, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error al listar runs de workflow '{Workflow}': {Status}", workflowFileName, response.StatusCode);
            return [];
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!doc.RootElement.TryGetProperty("workflow_runs", out var runsElement) || runsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<GitHubWorkflowRunDto>();
        foreach (var run in runsElement.EnumerateArray())
        {
            var id = run.GetProperty("id").GetInt64();
            var attempt = run.TryGetProperty("run_attempt", out var att) ? att.GetInt32() : 1;
            var name = run.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var headSha = run.TryGetProperty("head_sha", out var sha) ? sha.GetString() ?? "" : "";
            var status = run.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
            var conclusion = run.TryGetProperty("conclusion", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            var createdAt = run.TryGetProperty("created_at", out var ca) ? ca.GetDateTimeOffset() : DateTimeOffset.UtcNow;
            var updatedAt = run.TryGetProperty("updated_at", out var ua) ? ua.GetDateTimeOffset() : DateTimeOffset.UtcNow;

            result.Add(new GitHubWorkflowRunDto(id, attempt, name, headSha, status, conclusion, createdAt, updatedAt));
        }

        return result;
    }

    public async Task<Stream?> DownloadArtifactAsync(
        long runId,
        string artifactName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            return null;
        }

        var artifactsEndpoint = $"repos/{_repository}/actions/runs/{runId}/artifacts";
        var response = await _httpClient.GetAsync(artifactsEndpoint, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!doc.RootElement.TryGetProperty("artifacts", out var artifactsElement))
        {
            return null;
        }

        long? targetArtifactId = null;
        foreach (var art in artifactsElement.EnumerateArray())
        {
            var name = art.GetProperty("name").GetString();
            if (string.Equals(name, artifactName, StringComparison.OrdinalIgnoreCase))
            {
                targetArtifactId = art.GetProperty("id").GetInt64();
                break;
            }
        }

        if (targetArtifactId == null)
        {
            _logger.LogWarning("Artefacto '{Name}' no encontrado en run {RunId}", artifactName, runId);
            return null;
        }

        var downloadEndpoint = $"repos/{_repository}/actions/artifacts/{targetArtifactId}/zip";
        var downloadResponse = await _httpClient.GetAsync(downloadEndpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!downloadResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Error al descargar archivo ZIP del artefacto {ArtifactId}: {Status}",
                targetArtifactId, downloadResponse.StatusCode);
            return null;
        }

        return await downloadResponse.Content.ReadAsStreamAsync(cancellationToken);
    }
}
