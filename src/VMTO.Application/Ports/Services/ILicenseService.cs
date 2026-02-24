using VMTO.Domain.Aggregates.License;
using VMTO.Shared;

namespace VMTO.Application.Ports.Services;

public interface ILicenseService
{
    Task<Result<License>> ValidateAsync(string licenseKey, CancellationToken ct = default);
    Task<Result> ActivateAsync(string licenseKey, Dictionary<string, string> bindings, CancellationToken ct = default);
    Task<Result<bool>> CheckFeatureAsync(string feature, CancellationToken ct = default);
    Task<Result<int>> GetConcurrentJobLimitAsync(CancellationToken ct = default);
}
