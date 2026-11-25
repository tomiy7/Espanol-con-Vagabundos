using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using NotificationsService.API.Data;
using NotificationsService.API.Recipients;
using RabbitMQ.Client;

namespace NotificationsService.Tests.Infrastructure;

// Pravi Notifications servis nad pravim Postgres-om, RabbitMQ-om i Mailpit-om (hvata mejlove).
// Env: NOTIFICATIONS_TEST_CONNECTION (Postgres), NOTIFICATIONS_TEST_RABBITMQ
// (amqp://user:pass@host:port), NOTIFICATIONS_TEST_MAILPIT (http://host:8025) i
// NOTIFICATIONS_TEST_SMTP_PORT (SMTP port Mailpit-a, podrazumevano 1025).
// Bez njih se integracioni testovi preskacu. Auth i Courses su zamenjeni lazima.
public class NotificationsFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestExchange = "coursesite-tests.events";
    public const string TestQueue = "notifications-tests";
    private const string Database = "notifications_integration_tests";

    private static string? Env(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;

    public static string? ConnectionString => Env("NOTIFICATIONS_TEST_CONNECTION");
    public static Uri? RabbitMqUri => Env("NOTIFICATIONS_TEST_RABBITMQ") is { } v ? new Uri(v) : null;
    public static Uri? MailpitUri => Env("NOTIFICATIONS_TEST_MAILPIT") is { } v ? new Uri(v) : null;
    public static int SmtpPort => int.TryParse(Env("NOTIFICATIONS_TEST_SMTP_PORT"), out var p) ? p : 1025;

    public static bool IsConfigured => ConnectionString is not null && RabbitMqUri is not null && MailpitUri is not null;

    public FakeUsers Users { get; } = new();
    public FakeCourses Courses { get; } = new();
    public Mailpit Mail { get; } = new();

    public async Task InitializeAsync()
    {
        if (!IsConfigured) return;

        var admin = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" }.ConnectionString;
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS {Database} WITH (FORCE)", connection).ExecuteNonQueryAsync();
            await new NpgsqlCommand($"CREATE DATABASE {Database}", connection).ExecuteNonQueryAsync();
        }

        var rabbit = RabbitMqUri!;
        var userInfo = rabbit.UserInfo.Split(':', 2);
        Environment.SetEnvironmentVariable("ConnectionStrings__Default",
            new NpgsqlConnectionStringBuilder(ConnectionString) { Database = Database }.ConnectionString);
        Environment.SetEnvironmentVariable("RabbitMq__Enabled", "true");
        Environment.SetEnvironmentVariable("RabbitMq__HostName", rabbit.Host);
        Environment.SetEnvironmentVariable("RabbitMq__Port", rabbit.Port.ToString());
        Environment.SetEnvironmentVariable("RabbitMq__UserName", Uri.UnescapeDataString(userInfo[0]));
        Environment.SetEnvironmentVariable("RabbitMq__Password", userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "");
        Environment.SetEnvironmentVariable("RabbitMq__Exchange", TestExchange);
        Environment.SetEnvironmentVariable("RabbitMq__QueueName", TestQueue);
        Environment.SetEnvironmentVariable("RabbitMq__MaxDeliveryAttempts", "3");
        Environment.SetEnvironmentVariable("Smtp__Host", MailpitUri!.Host);
        Environment.SetEnvironmentVariable("Smtp__Port", SmtpPort.ToString());
        Environment.SetEnvironmentVariable("Notifications__FrontendBaseUrl", "http://front.test");

        await using (var bus = await TestBus.ConnectAsync())
        {
            await bus.DeleteQueueAsync(TestQueue);
            await bus.DeleteQueueAsync(TestQueue + ".dead");
        }

        using (var scope = Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();

        await TestBus.WaitForConsumerAsync(TestQueue);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserDirectory>();
            services.RemoveAll<ICourseDirectory>();
            services.AddSingleton<IUserDirectory>(Users);
            services.AddSingleton<ICourseDirectory>(Courses);
        });
    }

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(ConnectionString) { Database = Database }.ConnectionString);
        await connection.OpenAsync();
        await new NpgsqlCommand("TRUNCATE TABLE sent_notifications", connection).ExecuteNonQueryAsync();
        await Mail.ClearAsync();
        Users.Clear();
        Courses.Clear();
    }

    public List<SentNotification> Sent()
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().SentNotifications.AsNoTracking().ToList();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (IsConfigured) await base.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class NotificationsCollection : ICollectionFixture<NotificationsFactory>
{
    public const string Name = "notifications";
}

public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (!NotificationsFactory.IsConfigured)
            Skip = "Set NOTIFICATIONS_TEST_CONNECTION, NOTIFICATIONS_TEST_RABBITMQ and NOTIFICATIONS_TEST_MAILPIT to run integration tests.";
    }
}

// Auth zamena: poznati korisnici imaju mejl; za "nedostupne" baca gresku kao pravi klijent.
public sealed class FakeUsers : IUserDirectory
{
    private readonly ConcurrentDictionary<Guid, UserContact> _users = new();
    private readonly ConcurrentDictionary<Guid, int> _unavailable = new();

    public UserContact Add(string name)
    {
        var id = Guid.NewGuid();
        var contact = new UserContact(id, $"{name.ToLowerInvariant()}.{id:N}@test.local", name);
        _users[id] = contact;
        return contact;
    }

    public void MakeUnavailable(Guid userId) => _unavailable[userId] = 0;
    public int Calls(Guid userId) => _unavailable.GetValueOrDefault(userId);

    public void Clear()
    {
        _users.Clear();
        _unavailable.Clear();
    }

    public Task<UserContact?> GetUserContactAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (_unavailable.ContainsKey(userId))
        {
            _unavailable.AddOrUpdate(userId, 1, (_, c) => c + 1);
            throw new HttpRequestException("Auth service is unavailable (test).");
        }
        return Task.FromResult(_users.TryGetValue(userId, out var contact) ? contact : null);
    }
}

public sealed class FakeCourses : ICourseDirectory
{
    private readonly ConcurrentDictionary<Guid, string> _courses = new();

    public Guid Add(string name)
    {
        var id = Guid.NewGuid();
        _courses[id] = name;
        return id;
    }

    public void Clear() => _courses.Clear();

    public Task<string?> GetCourseNameAsync(Guid courseId, CancellationToken cancellationToken) =>
        Task.FromResult(_courses.TryGetValue(courseId, out var name) ? name : null);
}

// Mailpit HTTP API: lista primljenih mejlova i njihov sadrzaj.
public sealed class Mailpit
{
    public sealed record Mail(string Id, string To, string Subject, string Text, string Html);

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private Uri Api(string path) => new(NotificationsFactory.MailpitUri!, path);

    public async Task ClearAsync() => await _http.DeleteAsync(Api("/api/v1/messages"));

    public async Task<List<Mail>> AllAsync()
    {
        using var list = JsonDocument.Parse(await _http.GetStringAsync(Api("/api/v1/messages?limit=500")));
        var result = new List<Mail>();
        foreach (var item in list.RootElement.GetProperty("messages").EnumerateArray())
        {
            var id = item.GetProperty("ID").GetString()!;
            using var full = JsonDocument.Parse(await _http.GetStringAsync(Api($"/api/v1/message/{id}")));
            var root = full.RootElement;
            result.Add(new Mail(id,
                root.GetProperty("To")[0].GetProperty("Address").GetString()!,
                root.GetProperty("Subject").GetString()!,
                root.GetProperty("Text").GetString() ?? "",
                root.GetProperty("HTML").GetString() ?? ""));
        }
        return result;
    }

    public async Task<List<Mail>> ToAsync(string address) =>
        (await AllAsync()).Where(m => string.Equals(m.To, address, StringComparison.OrdinalIgnoreCase)).ToList();

    // Ceka da stigne tacno onoliko mejlova koliko se ocekuje (pa jos malo, da se vidi da nema viska).
    public async Task<List<Mail>> WaitForAsync(string address, int count, int seconds = 15)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        List<Mail> mails = new();
        while (DateTime.UtcNow < until)
        {
            mails = await ToAsync(address);
            if (mails.Count >= count) break;
            await Task.Delay(200);
        }
        Assert.True(mails.Count >= count, $"Expected {count} mail(s) to {address}, got {mails.Count}.");
        await Task.Delay(500);
        return await ToAsync(address);
    }
}

// Objavljuje poruke kao Groups (isti omotac), i ceka potrosaca.
public sealed class TestBus : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _exchange;

    private TestBus(IConnection connection, IChannel channel, string exchange)
    {
        _connection = connection;
        _channel = channel;
        _exchange = exchange;
    }

    public static async Task<TestBus> ConnectAsync(string exchange = NotificationsFactory.TestExchange)
    {
        var connection = await new ConnectionFactory { Uri = NotificationsFactory.RabbitMqUri! }.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true);
        return new TestBus(connection, channel, exchange);
    }

    public Task DeleteQueueAsync(string queue) => _channel.QueueDeleteAsync(queue, ifUnused: false, ifEmpty: false);

    public Task<uint> MessageCountAsync(string queue) => _channel.MessageCountAsync(queue);

    public async Task PublishAsync(IntegrationEvent integrationEvent) =>
        await _channel.BasicPublishAsync(_exchange, EventEnvelope.TypeOf(integrationEvent.GetType()),
            JsonSerializer.SerializeToUtf8Bytes(EventEnvelope.From(integrationEvent), EventEnvelope.JsonOptions));

    // Potrosac pravi red i vezivanja na startu; poruka poslata pre toga bi se izgubila.
    public static async Task WaitForConsumerAsync(string queue, string exchange = NotificationsFactory.TestExchange)
    {
        var until = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < until)
        {
            await using var probe = await ConnectAsync(exchange);
            try
            {
                if (await probe._channel.ConsumerCountAsync(queue) > 0) return;
            }
            catch (Exception)
            {
                // red jos ne postoji
            }
            await Task.Delay(200);
        }
        throw new TimeoutException($"No consumer attached to {queue}.");
    }

    public async ValueTask DisposeAsync()
    {
        try { await _channel.DisposeAsync(); } catch (Exception) { /* kanal zatvoren posle neuspele provere */ }
        await _connection.DisposeAsync();
    }
}
