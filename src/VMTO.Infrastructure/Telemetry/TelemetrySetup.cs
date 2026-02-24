using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using VMTO.Shared.Telemetry;

namespace VMTO.Infrastructure.Telemetry;

public static class TelemetrySetup
{
    public static IServiceCollection AddVmtoTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .AddSource(ActivitySources.Name)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            })
            .WithMetrics(builder =>
            {
                builder
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddPrometheusExporter();
            });

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();

        services.AddLogging(builder => builder.AddSerilog(dispose: true));

        var connectionString = configuration.GetConnectionString("PostgreSQL");
        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddHealthChecks()
                .AddNpgSql(connectionString, name: "postgresql");
        }

        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddHealthChecks()
                .AddRedis(redisConnectionString, name: "redis");
        }

        return services;
    }
}
