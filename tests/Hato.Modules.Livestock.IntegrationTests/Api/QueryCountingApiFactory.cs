namespace Hato.Modules.Livestock.IntegrationTests.Api;

/// <summary>
/// docs/spec/feature-0001-admin-web-animal-groups/spec.md sec.8: exposes the ephemeral
/// Postgres connection string set up by <see cref="HatoApiFactory"/> so a test can build
/// its own <c>LivestockDbContext</c> (wired with a <see cref="QueryCountingInterceptor"/>)
/// pointed at the same database, without threading the interceptor through the app's DI
/// container. Mirrors the <see cref="JwtHatoApiFactory"/> precedent of subclassing rather
/// than modifying the shared base fixture, so other tests using <see cref="HatoApiFactory"/>
/// directly are unaffected.
/// </summary>
public class QueryCountingApiFactory : HatoApiFactory
{
    public string ConnectionStringForTests => ConnectionString;
}
