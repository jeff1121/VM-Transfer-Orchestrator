using Microsoft.EntityFrameworkCore;
using VMTO.Application.Ports.Repositories;
using VMTO.Domain.Aggregates.Artifact;

namespace VMTO.Infrastructure.Persistence.Repositories;

public sealed class ArtifactRepository : IArtifactRepository
{
    private readonly AppDbContext _db;

    public ArtifactRepository(AppDbContext db) => _db = db;

    public async Task<Artifact?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Artifacts.FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<IReadOnlyList<Artifact>> ListByJobIdAsync(Guid jobId, CancellationToken ct = default)
    {
        return await _db.Artifacts
            .Where(a => a.JobId == jobId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Artifact artifact, CancellationToken ct = default)
    {
        await _db.Artifacts.AddAsync(artifact, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Artifacts.FindAsync([id], ct);
        if (entity is not null)
        {
            _db.Artifacts.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<int> CountByJobIdAsync(Guid jobId, CancellationToken ct = default)
    {
        return await _db.Artifacts.CountAsync(a => a.JobId == jobId, ct);
    }
}
