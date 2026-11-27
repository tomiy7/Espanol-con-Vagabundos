using CoursesService.API.Access;
using CoursesService.API.Data;
using CoursesService.API.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace CoursesService.Tests.Infrastructure;

// Pokrece pravi Courses API nad POSEBNOM test bazom (env COURSES_TEST_CONNECTION).
// U produkciji semu pravi Directus; ovde se pravi iz EF modela (EnsureCreated), pa testovi
// proveravaju API nad nazivima kolona iz modela. Groups je zamenjen laznim klijentom.
public class CoursesFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtSecret = "courses-tests-secret-courses-tests-secret-123";
    public const string TestDatabaseName = "courses_integration_tests";
    private const string ConnectionVariable = "COURSES_TEST_CONNECTION";

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable));

    public static string NotConfiguredMessage =>
        $"Set {ConnectionVariable} to a Postgres connection string to run integration tests " +
        $"(they create and use their own database '{TestDatabaseName}').";

    private string _connectionString = string.Empty;

    public FakeGroupsAccess Groups { get; } = new();

    public async Task InitializeAsync()
    {
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

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _connectionString);
        Environment.SetEnvironmentVariable("Jwt__Secret", JwtSecret);

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGroupsAccessClient>();
            services.AddSingleton<IGroupsAccessClient>(Groups);
        });
    }

    // Brise podatke izmedju testova (tabele ostaju).
    public async Task ResetAsync()
    {
        Groups.Reset();
        if (!IsConfigured) return;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand("TRUNCATE TABLE tasks, sections, lessons, courses CASCADE", connection);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SeedAsync(params object[] entities)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(entities);
        await db.SaveChangesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();
}

[CollectionDefinition(Name)]
public class CoursesCollection : ICollectionFixture<CoursesFactory>
{
    public const string Name = "courses";
}

// Bez baze se integracioni testovi preskacu sa jasnom porukom.
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (!CoursesFactory.IsConfigured) Skip = CoursesFactory.NotConfiguredMessage;
    }
}

// Lazni Groups: odgovor po kursu ili simulirani kvar. Pamti token sa kojim je pitan.
public sealed class FakeGroupsAccess : IGroupsAccessClient
{
    private readonly Dictionary<Guid, CourseAccess> _access = new();

    public bool Unavailable { get; set; }
    public bool RejectsToken { get; set; }
    public string? LastToken { get; private set; }
    public int Calls { get; private set; }

    public void Grant(Guid courseId, int unlockedLessons) =>
        _access[courseId] = new CourseAccess(true, Guid.NewGuid(), unlockedLessons);

    public void Reset()
    {
        _access.Clear();
        Unavailable = false;
        RejectsToken = false;
        LastToken = null;
        Calls = 0;
    }

    public Task<CourseAccess> CheckAsync(string bearerToken, Guid courseId, CancellationToken cancellationToken)
    {
        Calls++;
        LastToken = bearerToken;
        if (Unavailable) throw new GroupsUnavailableException("Groups is down (test).");
        if (RejectsToken) throw new GroupsRejectedTokenException();
        return Task.FromResult(_access.TryGetValue(courseId, out var a) ? a : new CourseAccess(false, null, 0));
    }
}

// Kursevi i lekcije sa podrazumevanim vrednostima (Directus bi popunio datume).
public static class Seed
{
    public static Course Course(bool published = true) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Spanski A1",
        Slug = "spanski-" + Guid.NewGuid().ToString("N")[..8],
        IsPublished = published,
        Price = 9000,
        Level = "A1",
        DurationWeeks = 12,
        CreatedAt = DateTime.UtcNow
    };

    public static Lesson Lesson(Course course, int order, bool published = true, bool visible = true) => new()
    {
        Id = Guid.NewGuid(),
        CourseId = course.Id,
        Title = $"Lekcija {order}",
        Order = order,
        RecordingUrl = $"https://video.test/{order}",
        IsPublished = published,
        IsVisible = visible,
        DateCreated = DateTime.UtcNow
    };

    public static Section Section(Lesson lesson, int order, string? content) => new()
    {
        Id = Guid.NewGuid(),
        LessonId = lesson.Id,
        Title = $"Sekcija {order}",
        Order = order,
        Content = content,
        DateCreated = DateTime.UtcNow
    };
}
