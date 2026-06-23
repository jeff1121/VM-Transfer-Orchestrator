using VMTO.Application.DTOs;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Queries.Jobs;
using VMTO.Shared;

namespace VMTO.Application.Queries.Handlers;

/// <summary>
/// 處理取得單一遷移工作的查詢。
/// 載入 Job 後映射為 JobDto 回傳。
/// </summary>
public sealed class GetJobHandler : IQueryHandler<GetJobQuery, JobDto>
{
    private readonly IJobRepository _jobRepository;

    public GetJobHandler(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<Result<JobDto>> HandleAsync(GetJobQuery query, CancellationToken ct = default)
    {
        var job = await _jobRepository.GetByIdAsync(query.JobId, ct);
        if (job is null)
            return Result<JobDto>.Failure(ErrorCodes.Job.NotFound, $"找不到 Job {query.JobId}。");

        var stepDtos = job.Steps.Select(s => new JobStepDto(
            s.Id, s.Name, s.Order, s.Status, s.Progress, s.RetryCount, s.ErrorMessage)).ToList();

        var dto = new JobDto(
            job.Id,
            job.CorrelationId.Value,
            job.Strategy,
            job.Status,
            job.Progress,
            job.CreatedAt,
            job.UpdatedAt,
            stepDtos);

        return Result<JobDto>.Success(dto);
    }
}
