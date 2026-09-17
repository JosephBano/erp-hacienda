using System.Reflection;

namespace Hato.Api.Endpoints;

public static class VersionEndpoints
{
    public static void MapVersionEndpoints(this IEndpointRouteBuilder app)
    {
        var handler = () => Results.Ok(BuildInfo.Get());

        app.MapGet("/version", handler).AllowAnonymous().WithTags("Version");
        app.MapGet("/api/v1/version", handler).AllowAnonymous().WithTags("Version");
    }
}

public record VersionResponse(string Version, string Commit, string BuildDate);

public static class BuildInfo
{
    private static VersionResponse? _cached;

    public static VersionResponse Get()
    {
        if (_cached != null) return _cached;

        var assembly = Assembly.GetEntryAssembly() ?? typeof(BuildInfo).Assembly;
        var infoVer = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        string? version = Environment.GetEnvironmentVariable("HATO_VERSION")
            ?? Environment.GetEnvironmentVariable("BUILD_VERSION");
        string? commit = Environment.GetEnvironmentVariable("HATO_COMMIT")
            ?? Environment.GetEnvironmentVariable("BUILD_COMMIT");
        string? buildDate = Environment.GetEnvironmentVariable("HATO_BUILD_DATE")
            ?? Environment.GetEnvironmentVariable("BUILD_DATE");

        if (string.IsNullOrWhiteSpace(version))
        {
            var asmVer = assembly.GetName().Version;
            if (asmVer != null && (asmVer.Major != 1 || asmVer.Minor != 0 || asmVer.Build != 0))
            {
                version = $"{asmVer.Major}.{asmVer.Minor}.{asmVer.Build}";
            }
        }

        if (!string.IsNullOrWhiteSpace(infoVer))
        {
            var plusIdx = infoVer.IndexOf('+');
            if (plusIdx >= 0)
            {
                if (string.IsNullOrWhiteSpace(version))
                {
                    version = infoVer[..plusIdx];
                }

                var metadata = infoVer[(plusIdx + 1)..];
                var dotIdx = metadata.IndexOf('.');
                if (dotIdx >= 0)
                {
                    if (string.IsNullOrWhiteSpace(commit))
                    {
                        commit = metadata[..dotIdx];
                    }
                    if (string.IsNullOrWhiteSpace(buildDate))
                    {
                        buildDate = metadata[(dotIdx + 1)..];
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(commit))
                    {
                        commit = metadata;
                    }
                }
            }
            else if (string.IsNullOrWhiteSpace(version))
            {
                version = infoVer;
            }
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            var versionFilePath = Path.Combine(AppContext.BaseDirectory, "VERSION");
            if (File.Exists(versionFilePath))
            {
                version = File.ReadAllText(versionFilePath).Trim();
            }
            else if (File.Exists("VERSION"))
            {
                version = File.ReadAllText("VERSION").Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            version = "0.0.0";
        }

        if (string.IsNullOrWhiteSpace(commit))
        {
            commit = "local";
        }

        if (string.IsNullOrWhiteSpace(buildDate))
        {
            buildDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        _cached = new VersionResponse(version, commit, buildDate);
        return _cached;
    }
}
