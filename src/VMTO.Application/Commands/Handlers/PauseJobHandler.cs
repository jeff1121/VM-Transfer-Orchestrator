using VMTO.Application.Commands.Jobs;
using VMTO.Application.Ports.Repositories;
using VMTO.Shared;

namespace VMTO.Application.Commands.Handlers;

/// <summary>
/// 處理暫停遷移工作的命令。
/// 載入 Job 後呼叫 RequestPause() 執行狀態轉換。
/// </summary>
public sealed class PauseJobHandler : ICommandHandler<PauseJobCommand>
{
    private readonly IJobRepository _jobRepository;

    public PauseJobHandler(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<Result> HandleAsync(PauseJobCommand command, CancellationToken ct = default)
    {
        var job = await _jobRepository.GetByIdAsync(command.JobId, ct);
        if (job is null)
            return Result.Failure(ErrorCodes.Job.NotFound, $"找不到 Job {command.JobId}。");

        var result = job.RequestPause();
        if (!result.IsSuccess)
            return result;

        await _jobRepository.UpdateAsync(job, ct);
        return Result.Success();
    }
}
