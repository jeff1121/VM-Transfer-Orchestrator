using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using VMTO.Application.Ports.Services;
using VMTO.Domain.ValueObjects;
using VMTO.Infrastructure.Resilience;

namespace VMTO.Infrastructure.Storage;

public sealed class StorageAdapterFactory
{
    private readonly IConfiguration _configuration;
    private readonly IAmazonS3? _s3Client;
    private readonly CircuitBreakerNotifier _circuitBreakerNotifier;
    private readonly IChaosPolicy _chaosPolicy;
    private readonly RetryPolicyOptions _retryPolicyOptions;

    public StorageAdapterFactory(
        IConfiguration configuration,
        CircuitBreakerNotifier circuitBreakerNotifier,
        IChaosPolicy chaosPolicy,
        IOptions<RetryPolicyOptions> retryPolicyOptions,
        IAmazonS3? s3Client = null)
    {
        _configuration = configuration;
        _circuitBreakerNotifier = circuitBreakerNotifier;
        _chaosPolicy = chaosPolicy;
        _retryPolicyOptions = retryPolicyOptions.Value;
        _s3Client = s3Client;
    }

    public IStorageAdapter Create(StorageType storageType)
    {
        return storageType switch
        {
            StorageType.S3 or StorageType.CephS3 => CreateS3Adapter(),
            StorageType.Local or StorageType.CephRbd => CreateLocalAdapter(),
            _ => CreateLocalAdapter(),
        };
    }

    private S3StorageAdapter CreateS3Adapter()
    {
        if (_s3Client is null)
            throw new InvalidOperationException("S3 client is not configured.");

        var bucket = _configuration["Storage:S3:BucketName"] ?? "vmto-artifacts";
        return new S3StorageAdapter(_s3Client, bucket, _circuitBreakerNotifier, _retryPolicyOptions, _chaosPolicy);
    }

    private LocalStorageAdapter CreateLocalAdapter()
    {
        var basePath = _configuration["Storage:Local:BasePath"] ?? "/tmp/vmto-storage";
        return new LocalStorageAdapter(basePath);
    }
}
