using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.ValueObjects;

namespace VMTO.Application.Commands.Jobs;

public sealed record CreateJobCommand(
    Guid SourceConnectionId,
    Guid TargetConnectionId,
    StorageTarget StorageTarget,
    MigrationStrategy Strategy,
    MigrationOptions Options);
