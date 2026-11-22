using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using GroupsService.Application.Commands.AssignProfessorToSlot;
using GroupsService.Application.Commands.CancelEnrollment;
using GroupsService.Application.Commands.CompleteGroup;
using GroupsService.Application.Commands.CourseCompleted;
using GroupsService.Application.Commands.CreateGroup;
using GroupsService.Application.Commands.CreateIndividualEnrollment;
using GroupsService.Application.Commands.FreezeEnrollment;
using GroupsService.Application.Commands.InvitePartner;
using GroupsService.Application.Commands.JoinGroup;
using GroupsService.Application.Commands.RespondToInvitation;
using GroupsService.Application.Commands.StartGroup;
using GroupsService.Application.Interfaces;
using GroupsService.Application.Options;
using GroupsService.Application.Queries.CheckAccess;
using GroupsService.Application.Queries.GetAvailableGroups;
using GroupsService.Application.Queries.GetEnrollmentById;
using GroupsService.Application.Queries.GetGroupById;
using GroupsService.Application.Queries.GetGroups;
using GroupsService.Application.Queries.GetInvitationByToken;
using GroupsService.Application.Queries.GetMyEnrollments;
using GroupsService.Infrastructure.Clients;
using GroupsService.Infrastructure.Data;
using GroupsService.Infrastructure.Repository;
using GroupsService.API.Errors;

var builder = WebApplication.CreateBuilder(args);

// Tajne (lozinka baze, JWT tajna) nikad ne idu u appsettings.json - User Secrets lokalno,
// environment varijable u okruzenju. Bez njih servis odmah pada sa jasnom porukom.
var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? throw new InvalidOperationException(
                           "ConnectionStrings:Default is not set. Use User Secrets or environment variables.");

builder.Services.AddDbContext<GroupDbContext>(options => options.UseNpgsql(connectionString));

var jwtSecret = builder.Configuration["Jwt:Secret"]
                ?? throw new InvalidOperationException(
                    "Jwt:Secret is not set. Use User Secrets or environment variables.");

// Isti princip kao u Auth servisu (HS256, zajednicka tajna). Issuer i audience se za sada
// ne proveravaju kao ni u Auth-u - TODO kad se uvede RS256 / issuer i audience u tokenu.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod());
});

// Konfiguracija - "magicni brojevi" (prozori, max pokusaji) i adrese Auth/Courses servisa
builder.Services.Configure<EnrollmentOptions>(
    builder.Configuration.GetSection(EnrollmentOptions.SectionName));
builder.Services.Configure<AuthServiceOptions>(
    builder.Configuration.GetSection(AuthServiceOptions.SectionName));
builder.Services.Configure<CoursesServiceOptions>(
    builder.Configuration.GetSection(CoursesServiceOptions.SectionName));

// Infrastructure - repozitorijumi
builder.Services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();
builder.Services.AddScoped<IPartnerInvitationRepository, PartnerInvitationRepository>();
builder.Services.AddScoped<IAuditLog, AuditLog>();
builder.Services.AddScoped<IProfessorScheduleLock, ProfessorScheduleLock>();

// Infrastructure - HTTP klijenti ka Auth i Courses servisima (service-to-service).
// AddHttpClient upravlja zivotnim ciklusom HttpClient-a (pool, DNS) - bez 'new HttpClient()'.
// Kratak timeout: podaci odavde su best-effort (imena), pa nedostupan servis ne sme da
// drzi zahtev podrazumevanih 100 s.
builder.Services.AddHttpClient<IUserDirectory, AuthServiceUserDirectory>(c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddHttpClient<ICourseDirectory, CoursesServiceDirectory>(c => c.Timeout = TimeSpan.FromSeconds(5));

// Application - Command/Query handleri
builder.Services.AddScoped<CreateIndividualEnrollmentHandler>();
builder.Services.AddScoped<CreateGroupHandler>();
builder.Services.AddScoped<JoinGroupHandler>();
builder.Services.AddScoped<InvitePartnerHandler>();
builder.Services.AddScoped<AcceptInvitationHandler>();
builder.Services.AddScoped<DeclineInvitationHandler>();
builder.Services.AddScoped<AssignProfessorToSlotHandler>();
builder.Services.AddScoped<StartGroupHandler>();
builder.Services.AddScoped<CompleteGroupHandler>();
builder.Services.AddScoped<CourseCompletedHandler>();
builder.Services.AddScoped<CancelEnrollmentHandler>();
builder.Services.AddScoped<FreezeEnrollmentHandler>();
builder.Services.AddScoped<UnfreezeEnrollmentHandler>();
builder.Services.AddScoped<GetAvailableGroupsHandler>();
builder.Services.AddScoped<GetGroupsHandler>();
builder.Services.AddScoped<GetGroupByIdHandler>();
builder.Services.AddScoped<GetEnrollmentByIdHandler>();
builder.Services.AddScoped<GetMyEnrollmentsHandler>();
builder.Services.AddScoped<CheckAccessHandler>();
builder.Services.AddScoped<GetInvitationByTokenHandler>();

// TODO: CourseCompletedHandler se poziva tek kad Messaging (CourseCompleted iz
// Courses) bude u repou; do tada nema nicega sto ga pokrece.
// TODO: INotificationService / mejlovi (vidi TODO notifikacija u handlerima).

// Greske: sve odbijene operacije i neocekivani izuzeci postaju ProblemDetails sa stabilnim
// "code" poljem (vidi GroupsExceptionHandler i ErrorCodes). Kod se dodaje i automatskim
// odgovorima (npr. 400 za neispravan JSON) koji nemaju svoj.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions.TryAdd("code", ErrorCodes.ForStatus(context.ProblemDetails.Status)));
builder.Services.AddExceptionHandler<GroupsExceptionHandler>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();

// Provera zdravlja za Docker/CI/smoke testove: API je gore i baza je dostupna.
builder.Services.AddHealthChecks().AddDbContextCheck<GroupDbContext>();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT like this: Bearer {your token}"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Migracije se automatski primenjuju samo lokalno. U drugim okruzenjima se
    // primenjuju eksplicitnim korakom (vidi pravila za sema/migracije u CLAUDE.md).
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<GroupDbContext>().Database.Migrate();
}

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
// Potrebno da WebApplicationFactory<Program> iz integracionih testova moze da pokrene API.
public partial class Program;
