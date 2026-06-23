using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Infrastructure.Clients;
using VMTO.Infrastructure.Jobs;
using VMTO.Infrastructure.Notifications;
using VMTO.Infrastructure.Ops;
using VMTO.Infrastructure.Persistence;
using VMTO.Infrastructure.Persistence.Repositories;
using VMTO.Infrastructure.Resilience;
using VMTO.Infrastructure.Security;
using VMTO.Infrastructure.Storage;
using VMTO.Infrastructure.Telemetry;

namespace VMTO.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RetryPolicyOptions>(configuration.GetSection("Resilience:Retry"));
        services.Configure<ChaosOptions>(configuration.GetSection("Chaos"));
        services.Configure<OpsAutomationOptions>(configuration.GetSection("Ops"));
        services.AddSingleton<IChaosPolicy, ChaosPolicy>();
        services.AddSingleton<IOpsSnapshotStore, OpsSnapshotStore>();
        services.AddSingleton<IErrorClassifier, ErrorClassifier>();

        // EF Core + PostgreSQL
        var connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? "Host=localhost;Database=vmto;Username=vmto;Password=vmto";

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Repositories
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IConnectionRepository, ConnectionRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<ILicenseRepository, LicenseRepository>();

        // Storage
        var s3Endpoint = configuration["Storage:S3:Endpoint"];
        if (!string.IsNullOrEmpty(s3Endpoint))
        {
            services.AddSingleton<IAmazonS3>(sp =>
            {
                var config = new AmazonS3Config { ServiceURL = s3Endpoint, ForcePathStyle = true };
                return new AmazonS3Client(
                    configuration["Storage:S3:AccessKey"] ?? string.Empty,
                    configuration["Storage:S3:SecretKey"] ?? string.Empty,
                    config);
            });
        }

        services.AddScoped<StorageAdapterFactory>();

        // Encryption
        services.AddDataProtection();
        services.AddSingleton<IEncryptionService, DataProtectionEncryptionService>();

        // Notifications (SignalR)
        services.AddSignalR();
        services.AddScoped<INotificationService, SignalRNotificationService>();

        // Webhook 通知
        services.AddHttpClient("Webhook", c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<CircuitBreakerNotifier>();
        services.AddScoped<SelfHealingService>();
        services.AddScoped<ArtifactCleanupJob>();
        services.AddScoped<DailyReportJob>();
        services.AddScoped<StorageUsageJob>();
        services.AddScoped<HealthReportJob>();
        services.AddScoped<DatabaseBackupJob>();

        // Hypervisor clients
        var useMocks = configuration.GetValue<bool>("UseMockClients");
        if (useMocks)
        {
            services.AddSingleton<IVSphereClient, MockVSphereClient>();
            services.AddSingleton<IPveClient, MockPveClient>();
        }
        else
        {
            services.AddHttpClient<IVSphereClient, VSphereClient>();
            services.AddHttpClient<IPveClient, PveClient>();
        }

        // Qemu-img
        services.AddSingleton<IQemuImgService, QemuImgService>();

        // Audit logging
        services.AddScoped<IAuditLogService, AuditLogService>();

        // Telemetry, logging, health checks
        services.AddVmtoTelemetry(configuration);

        return services;
    }
}
