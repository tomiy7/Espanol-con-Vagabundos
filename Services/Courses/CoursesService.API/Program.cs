using System.Text;
using CoursesService.API.Access;
using CoursesService.API.Data;
using CoursesService.API.Errors;
using CoursesService.API.Options;
using CoursesService.API.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Sema baze (courses, lessons, sections, tasks) pripada Directus-u - ovde se samo cita.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret not found");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Token se cuva da bi se prosledio Groups-u pri proveri pristupa lekciji.
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

builder.Services.AddAuthorization();

// CORS - dozvoli frontend na localhost:3000
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod());
});

builder.Services.Configure<PricingOptions>(builder.Configuration.GetSection(PricingOptions.Section));
builder.Services.Configure<GroupsServiceOptions>(builder.Configuration.GetSection(GroupsServiceOptions.Section));

builder.Services.AddScoped<ICourseRepository, CourseRepository>();

// Pristup lekcijama odlucuje Groups (upis + broj otkljucanih lekcija).
builder.Services.AddHttpClient<IGroupsAccessClient, GroupsAccessClient>(c => c.Timeout = TimeSpan.FromSeconds(5));

// Greske: ProblemDetails sa stabilnim "code" poljem, i za odgovore koje pravi sam ASP.NET (401/403/404).
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions.TryAdd("code", ErrorCodes.ForStatus(context.ProblemDetails.Status)));

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Za WebApplicationFactory u testovima.
public partial class Program;
