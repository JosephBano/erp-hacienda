using Npgsql;
using Testcontainers.PostgreSql;

namespace Hato.TestSupport;

/// <summary>
/// The one place that decides where the real PostgreSQL of an integration test comes from.
///
/// Art. 12 and <c>AGENTS.md</c> rule 5 are not negotiable: persistence and API tests run
/// against a real PostgreSQL, never InMemory — half of what Fase 3.5 asserts (the
/// <c>CHECK</c> of ADR-0015, <c>jsonb</c> validation, the unique index that makes the push
/// idempotent, the recursive CTE of the pedigree) simply does not exist in a fake provider.
/// What *is* negotiable is how that PostgreSQL gets there, and that is what this class
/// makes configurable:
///
/// <list type="bullet">
///   <item><b>With <c>HATO_TEST_POSTGRES</c> set</b> — an already-running server is used
///   (the repo's own <c>docker-compose.yml</c>, the <c>services:</c> container in CI, or
///   anything else that speaks the protocol). Each fixture still gets a brand-new database
///   on that server, created here and dropped on dispose, so isolation is identical to a
///   container per fixture.</item>
///   <item><b>Without it</b> — Testcontainers starts a throwaway container, exactly as
///   before. Nothing changes for whoever has a working Docker daemon.</item>
/// </list>
///
/// The escape hatch exists because Testcontainers needs a daemon that can create bridge
/// networks, and there are real developer machines where it cannot (rootless setups,
/// sandboxes, some WSL configurations) — the failure reads
/// <c>failed to create endpoint testcontainers-ryuk-… operation not supported</c>. When
/// nobody on the team can run the integration suite locally, every fix is pushed blind and
/// CI turns into the debugger; that is the loop this class is here to break.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    /// <summary>
    /// Connection string of a server that already exists. When present, no container is
    /// started. The database named in it is only used to issue <c>CREATE DATABASE</c>;
    /// tests never touch it.
    /// </summary>
    public const string ExternalServerVariable = "HATO_TEST_POSTGRES";

    /// <summary>
    /// Connections each fixture may hold on a shared server. Deliberately small: the
    /// budget is the server's <c>max_connections</c> divided by however many fixtures the
    /// test run starts at once.
    /// </summary>
    private const int MaxPoolSizePerFixture = 5;

    private readonly PostgreSqlContainer? _container;
    private readonly string? _adminConnectionString;
    private readonly string? _databaseName;

    private TestDatabase(PostgreSqlContainer container, string connectionString)
    {
        _container = container;
        ConnectionString = connectionString;
    }

    private TestDatabase(string adminConnectionString, string databaseName, string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        _databaseName = databaseName;
        ConnectionString = connectionString;
    }

    /// <summary>Where this fixture's tests must point. Always a database of its own.</summary>
    public string ConnectionString { get; }

    /// <summary>True when an external server was used instead of a container.</summary>
    public bool UsesExternalServer => _container is null;

    public static async Task<TestDatabase> StartAsync(CancellationToken cancellationToken = default)
    {
        var external = Environment.GetEnvironmentVariable(ExternalServerVariable);

        return string.IsNullOrWhiteSpace(external)
            ? await StartContainerAsync(cancellationToken)
            : await CreateOnExternalServerAsync(external, cancellationToken);
    }

    private static async Task<TestDatabase> StartContainerAsync(CancellationToken cancellationToken)
    {
        var container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();

        await container.StartAsync(cancellationToken);

        return new TestDatabase(container, container.GetConnectionString());
    }

    private static async Task<TestDatabase> CreateOnExternalServerAsync(
        string adminConnectionString, CancellationToken cancellationToken)
    {
        // A name per fixture, not per run: several fixtures live at the same time when
        // xUnit runs assemblies in parallel, and two of them sharing a database would
        // trade container isolation for cross-test interference — the exact thing that
        // makes a suite flaky in a way nobody can reproduce.
        var databaseName = $"hato_test_{Guid.NewGuid():N}";

        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync(cancellationToken);
            await using var create = admin.CreateCommand();
            // The name is generated above from a GUID, so there is no interpolation of
            // anything a caller controls. Identifiers cannot be parameterized in DDL.
            create.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = databaseName,

            // A container per fixture had a whole server to itself; a shared server does
            // not. `dotnet test` runs every test project at once, so a dozen fixtures
            // pull from the same connection budget (PostgreSQL defaults to 100) and an
            // unbounded pool per fixture exhausts it — the symptom is not a failing
            // assertion but a test host that dies. A small pool is plenty: each fixture
            // drives one HTTP client at a time.
            MaxPoolSize = MaxPoolSizePerFixture,
        };

        return new TestDatabase(adminConnectionString, databaseName, builder.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        // Pooled connections would keep this database busy and make the DROP fail. Only
        // this fixture's pool is cleared: ClearAllPools would reach into the pools of
        // every other fixture running in parallel in the same process, which is how a
        // cleanup turns into a flaky suite.
        NpgsqlConnection.ClearPool(new NpgsqlConnection(ConnectionString));

        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)";

        try
        {
            await drop.ExecuteNonQueryAsync();
        }
        catch (PostgresException)
        {
            // Cleanup only. A database left behind on a disposable dev/CI server costs
            // nothing, while failing the run here would turn a green suite red for a
            // reason that has nothing to do with the code under test.
        }
    }
}
