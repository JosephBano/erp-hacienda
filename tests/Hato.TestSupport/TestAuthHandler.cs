using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hato.TestSupport;

/// <summary>
/// Fake authentication scheme for module integration tests that exercise business
/// behavior (Livestock, Production, Inventory, Breeding, Tasks), not the auth flow
/// itself — that is covered end-to-end by Hato.Modules.People.IntegrationTests with
/// real JWTs. Every request is authenticated as an Admin.
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    // Constants exposed so tests can assert against the identity the handler
    // injects without having to redeclare them. AL-01 (security audit #94):
    // the reception handler must persist the JWT subject's user id and full
    // name on every row — these constants let tests check that contract
    // exactly.
    public static readonly Guid AdminUserId = Guid.Empty;
    public const string AdminFullName = "test-runner";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, AdminUserId.ToString()),
            new Claim(ClaimTypes.Name, AdminFullName),
            new Claim(ClaimTypes.Role, "Admin"),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public static class TestAuthenticationExtensions
{
    /// <summary>Replaces the app's real JWT scheme with the always-Admin test scheme.</summary>
    public static void AddTestAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
    }
}
