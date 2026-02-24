using VMTO.Domain.Aggregates.Connection;

namespace VMTO.Application.Ports.Repositories;

public interface IConnectionRepository
{
    Task<Connection?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Connection>> ListAsync(int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Connection connection, CancellationToken ct = default);
    Task UpdateAsync(Connection connection, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
