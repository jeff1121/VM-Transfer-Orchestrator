using Hangfire;
using Hangfire.PostgreSql;
using MassTransit;
using VMTO.API.Endpoints;
using VMTO.API.Middleware;
using VMTO.Infrastructure;
using VMTO.Infrastructure.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure services (EF, Redis, clients, crypto, telemetry, health checks)
builder.Services.AddInfrastructure(builder.Configuration);

// MassTransit + RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("RabbitMQ") ?? "rabbitmq://localhost");
        cfg.ConfigureEndpoints(context);
    });
});

// Hangfire
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(
            builder.Configuration.GetConnectionString("Hangfire")
            ?? builder.Configuration.GetConnectionString("PostgreSQL"))));
builder.Services.AddHangfireServer();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "VMTO API", Version = "v1" });
});

// Response compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ProblemDetails + exception handler
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

// Middleware pipeline
app.UseResponseCompression();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Map endpoints
app.MapJobEndpoints();
app.MapConnectionEndpoints();
app.MapArtifactEndpoints();
app.MapLicenseEndpoints();

// SignalR hub
app.MapHub<MigrationHub>("/hubs/migration");

// Health checks
app.MapHealthChecks("/health");

// Hangfire dashboard (dev only)
if (app.Environment.IsDevelopment())
{
    app.MapHangfireDashboard("/hangfire");
}

app.Run();
