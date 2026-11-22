using GroupsService.Application.Interfaces;
using GroupsService.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace GroupsService.Integration.Tests.Infrastructure;

// Pokrece pravi API (pravi Program.cs, kontrolere, JWT, EF) nad POSEBNOM test bazom.
// Baza se pravi od nule i migrira pravom migracijom (InitialCreate), pa se i ona testira.
// Konekcija dolazi iz env varijable GROUPS_TEST_CONNECTION (server + kredencijali);
// naziv baze se uvek zamenjuje test bazom, pa se razvojna baza nikad ne dira.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtSecret = "integration-tests-secret-integration-tests-secret";
    public const string TestDatabaseName = "groups_integration_tests";
    private const string ConnectionVariable = "GROUPS_TEST_CONNECTION";

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable));

    public static string NotConfiguredMessage =>
        $"Set {ConnectionVariable} to a Postgres connection string to run integration tests " +
        $"(they create and use their own database '{TestDatabaseName}').";

    private string _connectionString = string.Empty;

    public FakeUserDirectory Users { get; } = new();
    public FakeCourseDirectory Courses { get; } = new();

    public async Task InitializeAsync()
    {
        // Bez baze testovi su preskoceni (IntegrationFact), pa ni fixture nema sta da radi.
        if (!IsConfigured) return;

        var raw = Environment.GetEnvironmentVariable(ConnectionVariable)!;
        var adminConnection = new NpgsqlConnectionStringBuilder(raw) { Database = "postgres" }.ConnectionString;
        _connectionString = new NpgsqlConnectionStringBuilder(raw) { Database = TestDatabaseName }.ConnectionString;

        await using (var admin = new NpgsqlConnection(adminConnection))
        {
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {TestDatabaseName} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {TestDatabaseName}", admin);
            await create.ExecuteNonQueryAsync();
        }

        // Program.cs cita konfiguraciju na samom startu, pa se podesava preko env varijabli.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _connectionString);
        Environment.SetEnvironmentVariable("Jwt__Secret", JwtSecret);

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<GroupDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" okruzenje: Program.cs ne primenjuje migracije sam, ovde ih primenjuje fixture.
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserDirectory>();
            services.RemoveAll<ICourseDirectory>();
            services.AddSingleton<IUserDirectory>(Users);
            services.AddSingleton<ICourseDirectory>(Courses);
        });
    }

    // Brise sve podatke izmedju testova (tabele ostaju), da testovi budu nezavisni.
    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "TRUNCATE TABLE audit_entries, partner_invitations, group_members, group_slots, groups, enrollments CASCADE",
            connection);
        await cmd.ExecuteNonQueryAsync();
        Users.Clear();
        Courses.Clear();
    }

    public T WithDb<T>(Func<GroupDbContext, T> action)
    {
        using var scope = Services.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<GroupDbContext>());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (IsConfigured) await base.DisposeAsync();
    }
}

// Jedna zajednicka fabrika (jedan API + jedna baza) za sve testove; testovi u kolekciji
// se izvrsavaju jedan po jedan, a baza se prazni pre svakog testa.
[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "groups-database";
}
