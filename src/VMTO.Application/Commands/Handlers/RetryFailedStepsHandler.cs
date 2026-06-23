using VMTO.Application.Commands.Jobs;
using VMTO.Application.Ports.Repositories;
using VMTO.Domain.Enums;
using VMTO.Shared;

namespace VMTO.Application.Commands.Handlers;

/// <summary>
/// 處理重試失敗步驟的命令。
/// 找出所有狀態為 Failed 的步驟並逐一呼叫 Retry()。
/// </summary>
public sealed class RetryFailedStepsHandler : ICommandHandler<RetryFailedStepsCommand>
{
    private readonly IJobRepository _jobRepository;

    public RetryFailedStepsHandler(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<Result> HandleAsync(RetryFailedStepsCommand command, CancellationToken ct = default)
    {
        var job = await _jobRepository.GetByIdAsync(command.JobId, ct);
        if (job is null)
            return Result.Failure(ErrorCodes.Job.NotFound, $"找不到 Job {command.JobId}。");

        var failedSteps = job.Steps.Where(s => s.Status == StepStatus.Failed).ToList();

        // 沒有失敗步驟時直接回傳成功
        if (failedSteps.Count == 0)
            return Result.Success();

        foreach (var step in failedSteps)
        {
            var result = step.Retry();
            if (!result.IsSuccess)
                return result;
        }

        await _jobRepository.UpdateAsync(job, ct);
        return Result.Success();
    }
}
