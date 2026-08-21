using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PaymentsService.Application.Commands.CancelPayment;
using PaymentsService.Application.Commands.ConfirmPayment;
using PaymentsService.Application.Commands.CreatePayment;
using PaymentsService.Application.Interfaces;
using PaymentsService.Application.Queries.GetPaymentById;
using PaymentsService.Application.Queries.GetPaymentsByCourse;
using PaymentsService.Application.Queries.GetPaymentsByUser;
using PaymentsService.Application.Queries.GetPendingPayments;
using PaymentsService.Infrastructure.Data;
using PaymentsService.Infrastructure.Documents;
using PaymentsService.Infrastructure.Qr;
using PaymentsService.Infrastructure.Repository;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var jwtSecret = builder.Configuration["Jwt:Secret"]
                ?? throw new InvalidOperationException("Jwt:Secret nije postavljen. Proveri appsettings.json, User Secrets, ili environment varijable.");

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

// Infrastructure
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IQrCodeGenerator, IpsQrCodeGenerator>();
builder.Services.AddScoped<IPaymentSlipGenerator, PaymentSlipGenerator>();

// Application (Command/Query handleri)
builder.Services.AddScoped<CreatePaymentHandler>();
builder.Services.AddScoped<ConfirmPaymentHandler>();
builder.Services.AddScoped<CancelPaymentHandler>();
builder.Services.AddScoped<GetPendingPaymentsHandler>();
builder.Services.AddScoped<GetPaymentByIdHandler>();
builder.Services.AddScoped<GetPaymentsByUserHandler>();
builder.Services.AddScoped<GetPaymentsByCourseHandler>();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
