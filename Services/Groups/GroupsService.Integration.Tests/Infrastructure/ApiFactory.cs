using GroupsService.Application.Interfaces;
using GroupsService.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Messaging.RabbitMq;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
    private const string RabbitMqVariable = "GROUPS_TEST_RABBITMQ";

    // Testovi poruka koriste svoj exchange i red, da se ne mesaju sa lokalnim compose-om.
    public const string TestExchange = "coursesite-tests.events";
    public const string TestQueue = "groups-tests";

    // RabbitMQ je opcion: bez njega se testovi poruka preskacu, a outbox se i dalje
    // proverava (poruke se upisuju, samo se ne salju). Format: amqp://user:pass@host:port
    public static Uri? RabbitMqUri =>
        Uri.TryCreate(Environment.GetEnvironmentVariable(RabbitMqVariable), UriKind.Absolute, out var uri) ? uri : null;

    public static string RabbitMqNotConfiguredMessage =>
        $"Set {RabbitMqVariable} (e.g. amqp://guest:guest@localhost:5672) to run messaging tests.";

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
        await ConfigureRabbitMqAsync();

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
            "TRUNCATE TABLE outbox_messages, audit_entries, partner_invitations, group_members, group_slots, groups, enrollments CASCADE",
            connection);
        await cmd.ExecuteNonQueryAsync();
        Users.Clear();
        Courses.Clear();
    }

    // Bez RabbitMQ-a slanje i slusanje su iskljuceni. Sa njim se red testova brise pre
    // starta, da ne ostanu poruke iz prethodnog pokretanja.
    private static async Task ConfigureRabbitMqAsync()
    {
        var uri = RabbitMqUri;
        Environment.SetEnvironmentVariable("RabbitMq__Enabled", (uri is not null).ToString());
        if (uri is null) return;

        var userInfo = uri.UserInfo.Split(':', 2);
        Environment.SetEnvironmentVariable("RabbitMq__HostName", uri.Host);
        Environment.SetEnvironmentVariable("RabbitMq__Port", uri.Port.ToString());
        Environment.SetEnvironmentVariable("RabbitMq__UserName", Uri.UnescapeDataString(userInfo[0]));
        Environment.SetEnvironmentVariable("RabbitMq__Password", userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "");
        Environment.SetEnvironmentVariable("RabbitMq__Exchange", TestExchange);
        Environment.SetEnvironmentVariable("RabbitMq__QueueName", TestQueue);
        Environment.SetEnvironmentVariable("RabbitMq__MaxDeliveryAttempts", "3");

        await using var bus = await TestBus.ConnectAsync();
        await bus.DeleteQueueAsync(TestQueue);
        await bus.DeleteQueueAsync(TestQueue + ".dead");
    }

    public T WithDb<T>(Func<GroupDbContext, T> action) => WithDb(this, action);

    public static T WithDb<T>(WebApplicationFactory<Program> factory, Func<GroupDbContext, T> action)
    {
        using var scope = factory.Services.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<GroupDbContext>());
    }

    // ---------- varijante API-ja za testove otkaza ----------

    // Drugi API proces nad istom konfiguracijom, sa zamenjenim servisima. Ima svoj host,
    // svoj potrosac i dispatcher, pa se gasi posle testa (await using).
    // Podrazumevano dobija i svoj exchange/red, da ne deli red sa glavnim API-jem.
    public WebApplicationFactory<Program> Variant(Action<IServiceCollection> configure) =>
        WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            UseMessaging(services, "coursesite-tests-variant.events", "groups-tests-variant");
            configure(services);
        }));

    public string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(_connectionString) { Database = database }.ConnectionString;

    // Prazna, migrirana baza za jedan test - kad glavni API ne sme da vidi podatke
    // (npr. da njegov dispatcher ne posalje poruke koje test namerno drzi neposlate).
    public async Task<string> CreateDatabaseAsync(string database)
    {
        await using (var admin = new NpgsqlConnection(ConnectionStringFor("postgres")))
        {
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin);
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = ConnectionStringFor(database);
        var options = new DbContextOptionsBuilder<GroupDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new GroupDbContext(options);
        await db.Database.MigrateAsync();
        return connectionString;
    }

    public async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DropDatabaseAsync(string database)
    {
        await using var admin = new NpgsqlConnection(ConnectionStringFor("postgres"));
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    // Gasi pozadinski servis varijante (npr. dispatcher, da ne salje tudje poruke).
    public static void RemoveHostedService<T>(IServiceCollection services)
    {
        foreach (var descriptor in services
                     .Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(T)).ToList())
            services.Remove(descriptor);
    }

    public static void UseDatabase(IServiceCollection services, string connectionString)
    {
        services.RemoveAll<IDbContextOptionsConfiguration<GroupDbContext>>();
        services.RemoveAll<DbContextOptions<GroupDbContext>>();
        services.AddDbContext<GroupDbContext>(options => options.UseNpgsql(connectionString));
    }

    // Varijanta dobija svoj exchange i red, da ne otima poruke glavnom API-ju.
    public static void UseMessaging(IServiceCollection services, string exchange, string queue, int? port = null) =>
        services.Configure<RabbitMqOptions>(options =>
        {
            options.Exchange = exchange;
            options.QueueName = queue;
            if (port is not null) options.Port = port.Value;
        });

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
