using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using Serilog;
using Serilog.Formatting.Json;
using VMTO.Shared.Telemetry;
using VMTO.Infrastructure.Telemetry.HealthChecks;

namespace VMTO.Infrastructure.Telemetry;

public static class TelemetrySetup
{
    public static IServiceCollection AddVmtoTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "vmto";
        var otlpEndpoint = configuration["OpenTelemetry:Otlp:Endpoint"];
        Uri? otlpEndpointUri = null;
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            if (!Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out otlpEndpointUri))
            {
                throw new InvalidOperationException("OpenTelemetry:Otlp:Endpoint must be a valid absolute URI.");
            }
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource =>
                resource.AddService(serviceName: serviceName, serviceVersion: ActivitySources.Version))
            .WithTracing(builder =>
            {
                builder
                    .AddSource(ActivitySources.Name)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                if (otlpEndpointUri is not null)
                {
                    builder.AddOtlpExporter(options => options.Endpoint = otlpEndpointUri);
                }
            })
            .WithMetrics(builder =>
            {
                builder
                    .AddMeter(ActivitySources.Name)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddPrometheusExporter();

                if (otlpEndpointUri is not null)
                {
                    builder.AddOtlpExporter(options => options.Endpoint = otlpEndpointUri);
                }
            });

        var logFilePath = configuration["Serilog:FilePath"] ?? Path.Combine("logs", $"{serviceName}-.json");
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithCorrelationIdHeader("X-Correlation-Id")
            .Enrich.With(new JobStepLogEnricher())
            .Enrich.With(new SensitiveDataMaskingEnricher())
            .WriteTo.Console(new JsonFormatter(renderMessage: true))
            .WriteTo.File(
                formatter: new JsonFormatter(renderMessage: true),
                path: logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        services.AddLogging(builder => builder.AddSerilog(dispose: true));

        var healthChecks = services.AddHealthChecks()
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
            .AddCheck<DiskSpaceHealthCheck>("disk_space", tags: ["ready"]);

        var connectionString = configuration.GetConnectionString("PostgreSQL");
        if (!string.IsNullOrEmpty(connectionString))
        {
            healthChecks
                .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"])
                .AddCheck<DbMigrationHealthCheck>("db_migrations", tags: ["ready"]);
        }

        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            healthChecks.AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);
        }

        var rabbitMqConnectionString = configuration.GetConnectionString("RabbitMQ");
        if (!string.IsNullOrEmpty(rabbitMqConnectionString))
        {
            services.AddSingleton<IConnection>(_ =>
            {
                var factory = new ConnectionFactory { Uri = new Uri(rabbitMqConnectionString) };
                return factory.CreateConnectionAsync().GetAwaiter().GetResult();
            });

            healthChecks
                .AddRabbitMQ(sp => sp.GetRequiredService<IConnection>(), name: "rabbitmq", tags: ["ready"])
                .AddCheck<RabbitMqQueueHealthCheck>("rabbitmq_queues", tags: ["ready"]);
        }

        if (!string.IsNullOrWhiteSpace(configuration["Storage:S3:Endpoint"]))
        {
            healthChecks.AddCheck<MinioHealthCheck>("minio", tags: ["ready"]);
        }

        return services;
    }
}
