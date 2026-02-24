using Microsoft.EntityFrameworkCore;
using VMTO.Application.Ports.Repositories;
using VMTO.Domain.Aggregates.Connection;

namespace VMTO.Infrastructure.Persistence.Repositories;

public sealed class ConnectionRepository : IConnectionRepository
{
    private readonly AppDbContext _db;

    public ConnectionRepository(AppDbContext db) => _db = db;

    public async Task<Connection?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Connections.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IReadOnlyList<Connection>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        return await _db.Connections
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Connection connection, CancellationToken ct = default)
    {
        await _db.Connections.AddAsync(connection, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Connection connection, CancellationToken ct = default)
    {
        _db.Connections.Update(connection);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Connections.FindAsync([id], ct);
        if (entity is not null)
        {
            _db.Connections.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await _db.Connections.CountAsync(ct);
    }
}
