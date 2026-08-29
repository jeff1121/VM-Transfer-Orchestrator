using VMTO.Application.Commands.Jobs;
using VMTO.Application.Ports.Repositories;
using VMTO.Application.Ports.Services;
using VMTO.Domain.Aggregates.Connection;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Planning;
using VMTO.Shared;
using VMTO.Shared.Telemetry;

namespace VMTO.Application.Commands.Handlers;

public sealed class CreateJobHandler : ICommandHandler<CreateJobCommand, Guid>
{
    private readonly IJobRepository _jobRepository;
    private readonly IConnectionRepository _connectionRepository;
    private readonly ISourcePlatformProviderFactory _sourceFactory;

    public CreateJobHandler(
        IJobRepository jobRepository,
        IConnectionRepository connectionRepository,
        ISourcePlatformProviderFactory sourceFactory)
    {
        _jobRepository = jobRepository;
        _connectionRepository = connectionRepository;
        _sourceFactory = sourceFactory;
    }

    public async Task<Result<Guid>> HandleAsync(CreateJobCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.VmId))
            return Result<Guid>.Failure(ErrorCodes.Plan.NoDisks, "VmId is required.");

        var source = await _connectionRepository.GetByIdAsync(command.SourceConnectionId, ct);
        if (source is null)
            return Result<Guid>.Failure(ErrorCodes.Connection.NotFound, $"找不到來源連線 {command.SourceConnectionId}。");

        var target = await _connectionRepository.GetByIdAsync(command.TargetConnectionId, ct);
        if (target is null)
            return Result<Guid>.Failure(ErrorCodes.Connection.NotFound, $"找不到目標連線 {command.TargetConnectionId}。");

        var diskKeys = await ResolveDiskKeysAsync(source, command, ct);
        if (!diskKeys.IsSuccess)
            return Result<Guid>.Failure(diskKeys.ErrorCode!, diskKeys.ErrorMessage!);

        var requestIncremental = command.Strategy == MigrationStrategy.Incremental;
        var planResult = MigrationPlanBuilder.Build(source.Type, target.Type, requestIncremental, diskKeys.Value!);
        if (!planResult.IsSuccess)
            return Result<Guid>.Failure(planResult.ErrorCode!, planResult.ErrorMessage!);

        var job = new MigrationJob(
            command.SourceConnectionId,
            command.TargetConnectionId,
            command.StorageTarget,
            MigrationPlanBuilder.DerivedStrategy(requestIncremental),
            command.Options,
            vmId: command.VmId,
            plan: planResult.Value);

        job.Enqueue();
        await _jobRepository.AddAsync(job, ct);
        VmtoMetrics.RecordJob("created", job.Strategy.ToString().ToLowerInvariant());
        return Result<Guid>.Success(job.Id);
    }

    private async Task<Result<IReadOnlyList<string>>> ResolveDiskKeysAsync(
        Connection source,
        CreateJobCommand command,
        CancellationToken ct)
    {
        if (command.DiskKeys is { Count: > 0 })
            return Result<IReadOnlyList<string>>.Success(command.DiskKeys);

        try
        {
            var provider = _sourceFactory.GetProvider(source.Type);
            var inspection = await provider.InspectAsync(source.Id, command.VmId, ct);
            if (inspection.IsSuccess && inspection.Value!.Disks.Count > 0)
                return Result<IReadOnlyList<string>>.Success(inspection.Value.Disks.Select(d => d.DiskKey).ToList());
        }
        catch (NotSupportedException ex)
        {
            return Result<IReadOnlyList<string>>.Failure(ErrorCodes.Plan.Incompatible, ex.Message);
        }

        return Result<IReadOnlyList<string>>.Success(["disk-0"]);
    }
}
