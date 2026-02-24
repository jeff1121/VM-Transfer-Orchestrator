using VMTO.Domain.Aggregates.License;

namespace VMTO.Application.Ports.Repositories;

public interface ILicenseRepository
{
    Task<License?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<License?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task AddAsync(License license, CancellationToken ct = default);
    Task UpdateAsync(License license, CancellationToken ct = default);
    Task<License?> GetActiveAsync(CancellationToken ct = default);
}
