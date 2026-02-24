using Microsoft.EntityFrameworkCore;
using VMTO.Application.Ports.Repositories;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;

namespace VMTO.Infrastructure.Persistence.Repositories;

public sealed class JobRepository : IJobRepository
{
    private readonly AppDbContext _db;

    public JobRepository(AppDbContext db) => _db = db;

    public async Task<MigrationJob?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Jobs
            .Include(j => j.Steps)
            .FirstOrDefaultAsync(j => j.Id == id, ct);
    }

    public async Task<IReadOnlyList<MigrationJob>> ListAsync(int page, int pageSize, JobStatus? status = null, CancellationToken ct = default)
    {
        var query = _db.Jobs.Include(j => j.Steps).AsQueryable();
        if (status.HasValue)
            query = query.Where(j => j.Status == status.Value);

        return await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task AddAsync(MigrationJob job, CancellationToken ct = default)
    {
        await _db.Jobs.AddAsync(job, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(MigrationJob job, CancellationToken ct = default)
    {
        _db.Jobs.Update(job);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> CountAsync(JobStatus? status = null, CancellationToken ct = default)
    {
        var query = _db.Jobs.AsQueryable();
        if (status.HasValue)
            query = query.Where(j => j.Status == status.Value);

        return await query.CountAsync(ct);
    }
}
