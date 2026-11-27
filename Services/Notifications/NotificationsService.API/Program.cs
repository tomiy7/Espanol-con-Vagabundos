using Messaging.Contracts;
using Messaging.RabbitMq;
using Microsoft.EntityFrameworkCore;
using NotificationsService.API.Data;
using NotificationsService.API.Email;
using NotificationsService.API.Handlers;
using NotificationsService.API.Options;
using NotificationsService.API.Recipients;
using NotificationsService.API.Sending;
using NotificationsService.API.Templates;

var builder = WebApplication.CreateBuilder(args);

// Notifications nema svoj API: slusa dogadjaje (RabbitMQ) i salje mejlove. HTTP sluzi samo
// za /health. Tajne (lozinka baze, SMTP, admin token za Auth) dolaze iz env/secrets.
var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? throw new InvalidOperationException(
                           "ConnectionStrings:Default is not set. Use User Secrets or environment variables.");
builder.Services.AddDbContext<NotificationsDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.Configure<NotificationsOptions>(builder.Configuration.GetSection(NotificationsOptions.SectionName));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<AuthServiceOptions>(builder.Configuration.GetSection(AuthServiceOptions.SectionName));
builder.Services.Configure<CoursesServiceOptions>(builder.Configuration.GetSection(CoursesServiceOptions.SectionName));

// Jezik se bira u konfiguraciji; nepodrzan jezik obara servis odmah na startu.
var templates = EmailTemplatesFactory.Create(
    builder.Configuration.GetSection(NotificationsOptions.SectionName).Get<NotificationsOptions>() ?? new NotificationsOptions());
builder.Services.AddSingleton(templates);

builder.Services.AddHttpClient<IUserDirectory, AuthServiceUserDirectory>(c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddHttpClient<ICourseDirectory, CoursesServiceDirectory>(c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<Notifier>();
builder.Services.AddScoped<NotificationContext>();

builder.Services.AddHealthChecks().AddDbContextCheck<NotificationsDbContext>();

var rabbitMq = builder.Configuration.GetSection(RabbitMqOptions.SectionName);
if (rabbitMq.GetValue(nameof(RabbitMqOptions.Enabled), true))
{
    builder.Services.AddRabbitMqMessaging(rabbitMq)
        .AddHandler<PartnerInvited, PartnerInvitedHandler>()
        .AddHandler<InvitationAccepted, InvitationAcceptedHandler>()
        .AddHandler<InvitationDeclined, InvitationDeclinedHandler>()
        .AddHandler<EnrollmentCreated, EnrollmentCreatedHandler>()
        .AddHandler<EnrollmentReturnedToSearching, EnrollmentReturnedToSearchingHandler>()
        .AddHandler<EnrollmentFrozen, EnrollmentFrozenHandler>()
        .AddHandler<EnrollmentUnfrozen, EnrollmentUnfrozenHandler>()
        .AddHandler<EnrollmentCancelled, EnrollmentCancelledHandler>()
        .AddHandler<EnrollmentCompleted, EnrollmentCompletedHandler>()
        .AddHandler<GroupBecameReady, GroupBecameReadyHandler>()
        .AddHandler<GroupStarted, GroupStartedHandler>()
        .AddHandler<GroupDissolved, GroupDissolvedHandler>()
        .AddHandler<FirstLessonRescheduled, FirstLessonRescheduledHandler>();
}

var app = builder.Build();

// Provera SMTP podesavanja bez ostatka sistema: salje primer mejla (pozivnicu) i izlazi.
//   docker compose run --rm notifications.api send-test-email adresa@primer.com
if (args is ["send-test-email", var testRecipient])
{
    using var scope = app.Services.CreateScope();
    var sample = scope.ServiceProvider.GetRequiredService<IEmailTemplates>().PartnerInvited(
        "Test", "Español con Vagabundos", "Probni kurs", "http://localhost:3000/invitations/test", DateTime.UtcNow.AddDays(5));
    await scope.ServiceProvider.GetRequiredService<IEmailSender>().SendAsync(
        testRecipient, null, sample with { Subject = "[TEST] " + sample.Subject }, CancellationToken.None);
    Console.WriteLine($"Test email sent to {testRecipient}.");
    return;
}

if (app.Environment.IsDevelopment())
{
    // Kao u Groups: migracije se same primenjuju samo lokalno.
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.Migrate();
}

app.MapHealthChecks("/health");

app.Run();

// Potrebno da WebApplicationFactory<Program> iz testova moze da pokrene servis.
public partial class Program;
