using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;

namespace VMTO.Application.Ports.Repositories;

public interface IJobRepository
{
    Task<MigrationJob?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<MigrationJob>> ListAsync(int page, int pageSize, JobStatus? status = null, CancellationToken ct = default);
    Task AddAsync(MigrationJob job, CancellationToken ct = default);
    Task UpdateAsync(MigrationJob job, CancellationToken ct = default);
    Task<int> CountAsync(JobStatus? status = null, CancellationToken ct = default);
}
