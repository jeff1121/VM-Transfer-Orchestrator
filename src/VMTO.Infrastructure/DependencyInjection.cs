using Amazon.S3;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Infrastructure.Clients;
using VMTO.Infrastructure.Notifications;
using VMTO.Infrastructure.Persistence;
using VMTO.Infrastructure.Persistence.Repositories;
using VMTO.Infrastructure.Security;
using VMTO.Infrastructure.Storage;
using VMTO.Infrastructure.Telemetry;

namespace VMTO.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // EF Core + PostgreSQL
        var connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? throw new InvalidOperationException("PostgreSQL connection string is required.");

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
                var s3Config = new AmazonS3Config { ServiceURL = s3Endpoint, ForcePathStyle = true };
                var accessKey = configuration["Storage:S3:AccessKey"]
                    ?? throw new InvalidOperationException("Storage:S3:AccessKey is required when S3 endpoint is configured.");
                var secretKey = configuration["Storage:S3:SecretKey"]
                    ?? throw new InvalidOperationException("Storage:S3:SecretKey is required when S3 endpoint is configured.");
                return new AmazonS3Client(accessKey, secretKey, s3Config);
            });
        }

        services.AddSingleton<StorageAdapterFactory>();

        // Encryption — 金鑰持久化至 FileSystem
        // 生產環境必須透過 volume mount 確保 KeysPath 目錄持久化，否則容器重啟後將無法解密既有資料
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(
                configuration["DataProtection:KeysPath"] ?? "/app/keys"))
            .SetApplicationName("VMTO");
        services.AddSingleton<IEncryptionService, DataProtectionEncryptionService>();

        // Notifications (SignalR)
        services.AddSignalR();
        services.AddScoped<INotificationService, SignalRNotificationService>();

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
