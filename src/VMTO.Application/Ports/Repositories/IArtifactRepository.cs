using VMTO.Domain.Aggregates.Artifact;

namespace VMTO.Application.Ports.Repositories;

public interface IArtifactRepository
{
    Task<Artifact?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Artifact>> ListByJobIdAsync(Guid jobId, CancellationToken ct = default);
    Task AddAsync(Artifact artifact, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> CountByJobIdAsync(Guid jobId, CancellationToken ct = default);
}
