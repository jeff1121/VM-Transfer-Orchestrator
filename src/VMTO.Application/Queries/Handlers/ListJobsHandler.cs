using VMTO.Application.DTOs;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Queries.Jobs;
using VMTO.Shared;

namespace VMTO.Application.Queries.Handlers;

/// <summary>
/// 處理列出遷移工作的查詢。
/// 支援分頁與狀態篩選，將結果映射為 JobDto 清單。
/// </summary>
public sealed class ListJobsHandler : IQueryHandler<ListJobsQuery, IReadOnlyList<JobDto>>
{
    private readonly IJobRepository _jobRepository;

    public ListJobsHandler(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<Result<IReadOnlyList<JobDto>>> HandleAsync(ListJobsQuery query, CancellationToken ct = default)
    {
        var jobs = await _jobRepository.ListAsync(query.Page, query.PageSize, query.Status, ct);

        var dtos = jobs.Select(job => new JobDto(
            job.Id,
            job.CorrelationId.Value,
            job.Strategy,
            job.Status,
            job.Progress,
            job.CreatedAt,
            job.UpdatedAt,
            job.Steps.Select(s => new JobStepDto(
                s.Id, s.Name, s.Order, s.Status, s.Progress, s.RetryCount, s.ErrorMessage)).ToList()))
            .ToList();

        return Result<IReadOnlyList<JobDto>>.Success(dtos);
    }
}
