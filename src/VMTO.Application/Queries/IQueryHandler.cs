using VMTO.Shared;

namespace VMTO.Application.Queries;

public interface IQueryHandler<TQuery, TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken ct = default);
}
