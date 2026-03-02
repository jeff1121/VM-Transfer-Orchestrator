using System.Security.Claims;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.PostgreSql;
using MassTransit;
using VMTO.API.Auth;
using VMTO.API.Endpoints;
using VMTO.API.Middleware;
using VMTO.Application;
using VMTO.Infrastructure;
using VMTO.Infrastructure.Hubs;

var builder = WebApplication.CreateBuilder(args);

// JWT 認證設定
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
// 開發環境若未設定 SecretKey，自動產生臨時金鑰
if (string.IsNullOrEmpty(jwtSettings.SecretKey))
{
    jwtSettings.SecretKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    builder.Configuration[$"{JwtSettings.SectionName}:SecretKey"] = jwtSettings.SecretKey;
}

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // SignalR JWT 支援：從 query string 取得 token
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// Application services (command/query handlers)
builder.Services.AddApplication();

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
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "輸入 JWT token"
    });
    c.AddSecurityRequirement(doc =>
    {
        var requirement = new Microsoft.OpenApi.OpenApiSecurityRequirement();
        var schemeRef = new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", doc);
        requirement.Add(schemeRef, new List<string>());
        return requirement;
    });
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

// Rate Limiting — 依角色不同限制
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // 全域預設策略：Fixed Window（每分鐘 60 次）
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var role = context.User.FindFirstValue(ClaimTypes.Role) ?? "anonymous";
        var permitLimit = role switch
        {
            Roles.Admin => 300,     // Admin 較高限制
            Roles.Operator => 120,  // Operator 中等限制
            _ => 60                 // Viewer / 匿名 較低限制
        };

        var partitionKey = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "authenticated"
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 5
        });
    });

    // 具名策略：寫入操作更嚴格（每分鐘 30 次）
    options.AddPolicy("write", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            }));

    // 具名策略：登入端點防暴力破解（每分鐘 10 次）
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        var correlationId = context.HttpContext.Items["CorrelationId"] as string;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new VMTO.API.Models.ErrorResponse("RATE_LIMIT_EXCEEDED", "請求頻率超過限制，請稍後再試", correlationId),
            cancellationToken);
    };
});

var app = builder.Build();

// Middleware pipeline
app.UseResponseCompression();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Map endpoints
app.MapAuthEndpoints();
app.MapJobEndpoints();
app.MapWebhookEndpoints();
app.MapConnectionEndpoints();
app.MapArtifactEndpoints();
app.MapLicenseEndpoints();
app.MapAuditEndpoints();
app.MapDashboardEndpoints();

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
