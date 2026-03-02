using VMTO.Application.Commands.Jobs;
using VMTO.Application.Ports.Repositories;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Strategies;
using VMTO.Shared;

namespace VMTO.Application.Commands.Handlers;

/// <summary>
/// 處理建立遷移工作的命令。
/// 根據策略列舉解析對應的 IMigrationStrategy，建立 MigrationJob 聚合並新增步驟後持久化。
/// </summary>
public sealed class CreateJobHandler : ICommandHandler<CreateJobCommand, Guid>
{
    private readonly IJobRepository _jobRepository;

    public CreateJobHandler(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<Result<Guid>> HandleAsync(CreateJobCommand command, CancellationToken ct = default)
    {
        // 根據策略列舉解析對應的遷移策略
        IMigrationStrategy strategy = command.Strategy switch
        {
            MigrationStrategy.FullCopy => new FullCopyStrategy(),
            MigrationStrategy.Incremental => new IncrementalStrategy(),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.Strategy, "不支援的遷移策略。")
        };

        var job = new MigrationJob(
            command.SourceConnectionId,
            command.TargetConnectionId,
            command.StorageTarget,
            command.Strategy,
            command.Options);

        // 依策略定義的步驟名稱逐一新增步驟
        var stepNames = strategy.GetStepNames();
        for (var i = 0; i < stepNames.Count; i++)
        {
            job.AddStep(stepNames[i], i + 1);
        }

        await _jobRepository.AddAsync(job, ct);
        return Result<Guid>.Success(job.Id);
    }
}
