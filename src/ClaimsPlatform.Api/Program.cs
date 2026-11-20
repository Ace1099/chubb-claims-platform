using System.Text.Json.Serialization;
using ClaimsPlatform.Api.Data;
using ClaimsPlatform.Api.Events;
using ClaimsPlatform.Api.Infrastructure;
using ClaimsPlatform.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("UserId", new OpenApiSecurityScheme
    {
        Name = "X-User-Id", In = ParameterLocation.Header, Type = SecuritySchemeType.ApiKey,
        Description = "Demo auth: paste a user id from GET /api/users"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "UserId" } }]
            = Array.Empty<string>()
    });
});

// Connection string is resolved lazily so tests can override it.
var defaultDb = $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "claims.db")}";
builder.Services.AddDbContext<ClaimsDbContext>((sp, o) =>
    o.UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default") ?? defaultDb));

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserContext>();
builder.Services.AddScoped<ClaimService>();
builder.Services.AddScoped<ReportingService>();
builder.Services.AddScoped<NotificationHandler>();

// ---- Event transport: in-process by default. The Kafka adapter is added in Step 10. ----
builder.Services.AddSingleton<InProcessEventBus>();
builder.Services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<InProcessEventBus>());
builder.Services.AddHostedService<InProcessEventConsumer>();

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Sprint shortcut: create schema + seed on startup (production: migrations).
using (var scope = app.Services.CreateScope())
{
    await SeedData.InitAsync(scope.ServiceProvider.GetRequiredService<ClaimsDbContext>(),
        app.Configuration.GetValue("Seed:SampleClaims", true));
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.Run();

public partial class Program { } // lets integration tests start the app