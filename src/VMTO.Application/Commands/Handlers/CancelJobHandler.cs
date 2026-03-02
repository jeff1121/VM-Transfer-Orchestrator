using VMTO.Application.Commands.Jobs;
using VMTO.Application.Ports.Repositories;
using VMTO.Shared;

namespace VMTO.Application.Commands.Handlers;

/// <summary>
/// 處理取消遷移工作的命令。
/// 載入 Job 後呼叫 RequestCancel() 執行狀態轉換。
/// </summary>
public sealed class CancelJobHandler : ICommandHandler<CancelJobCommand>
{
    private readonly IJobRepository _jobRepository;

    public CancelJobHandler(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<Result> HandleAsync(CancelJobCommand command, CancellationToken ct = default)
    {
        var job = await _jobRepository.GetByIdAsync(command.JobId, ct);
        if (job is null)
            return Result.Failure(ErrorCodes.Job.NotFound, $"找不到 Job {command.JobId}。");

        var result = job.RequestCancel();
        if (!result.IsSuccess)
            return result;

        await _jobRepository.UpdateAsync(job, ct);
        return Result.Success();
    }
}
