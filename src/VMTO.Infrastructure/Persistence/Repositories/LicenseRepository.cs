using Microsoft.EntityFrameworkCore;
using VMTO.Application.Ports.Repositories;
using VMTO.Domain.Aggregates.License;

namespace VMTO.Infrastructure.Persistence.Repositories;

public sealed class LicenseRepository : ILicenseRepository
{
    private readonly AppDbContext _db;

    public LicenseRepository(AppDbContext db) => _db = db;

    public async Task<License?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Licenses.FirstOrDefaultAsync(l => l.Id == id, ct);
    }

    public async Task<License?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        return await _db.Licenses.FirstOrDefaultAsync(l => l.Key == key, ct);
    }

    public async Task AddAsync(License license, CancellationToken ct = default)
    {
        await _db.Licenses.AddAsync(license, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(License license, CancellationToken ct = default)
    {
        _db.Licenses.Update(license);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<License?> GetActiveAsync(CancellationToken ct = default)
    {
        return await _db.Licenses
            .Where(l => l.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}
