using VMTO.Application.DTOs;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Queries.Jobs;
using VMTO.Shared;

namespace VMTO.Application.Queries.Handlers;

/// <summary>
/// 處理取得遷移工作進度的查詢。
/// 載入 Job 後映射為 JobProgressDto 回傳。
/// </summary>
public sealed class GetJobProgressHandler : IQueryHandler<GetJobProgressQuery, JobProgressDto>
{
    private readonly IJobRepository _jobRepository;

    public GetJobProgressHandler(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<Result<JobProgressDto>> HandleAsync(GetJobProgressQuery query, CancellationToken ct = default)
    {
        var job = await _jobRepository.GetByIdAsync(query.JobId, ct);
        if (job is null)
            return Result<JobProgressDto>.Failure(ErrorCodes.Job.NotFound, $"找不到 Job {query.JobId}。");

        var stepDtos = job.Steps.Select(s => new JobStepDto(
            s.Id, s.Name, s.Order, s.Status, s.Progress, s.RetryCount, s.ErrorMessage)).ToList();

        var dto = new JobProgressDto(job.Id, job.Status, job.Progress, stepDtos);
        return Result<JobProgressDto>.Success(dto);
    }
}
