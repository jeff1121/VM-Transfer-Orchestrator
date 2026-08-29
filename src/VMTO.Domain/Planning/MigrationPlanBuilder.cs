using VMTO.Domain.Aggregates.Connection;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;
using VMTO.Domain.ValueObjects;
using VMTO.Shared;

namespace VMTO.Domain.Planning;

public static class MigrationPlanBuilder
{
    public const int PlanVersion = 1;

    public static Result<MigrationPlan> Build(
        PlatformKind source,
        PlatformKind target,
        bool requestIncremental,
        IReadOnlyList<string> diskKeys)
    {
        if (diskKeys.Count == 0)
            return Result<MigrationPlan>.Failure(ErrorCodes.Plan.NoDisks, "A migration plan requires at least one disk.");

        var sourceCaps = PlatformCapabilityCatalog.For(source);
        var targetCaps = PlatformCapabilityCatalog.For(target);

        if (!sourceCaps.CanBeSource)
            return Result<MigrationPlan>.Failure(ErrorCodes.Plan.Incompatible, $"{source} cannot be a Source.");

        if (!targetCaps.CanBeTarget)
            return Result<MigrationPlan>.Failure(ErrorCodes.Plan.Incompatible, $"{target} cannot be a Target.");

        if (requestIncremental && !sourceCaps.SupportsIncrementalExport)
            return Result<MigrationPlan>.Failure(
                ErrorCodes.Plan.IncrementalNotSupported,
                $"{source} does not support incremental export.");

        var steps = requestIncremental
            ? BuildIncrementalSteps()
            : BuildOfflineFullCopySteps(diskKeys);

        var plan = new MigrationPlan(
            PlanVersion,
            source,
            target,
            SourceAdapterId: source.ToString(),
            TargetAdapterId: target.ToString(),
            steps);

        return Result<MigrationPlan>.Success(plan);
    }

    public static MigrationStrategy DerivedStrategy(bool requestIncremental) =>
        requestIncremental ? MigrationStrategy.Incremental : MigrationStrategy.FullCopy;

    private static List<PlannedStep> BuildOfflineFullCopySteps(IReadOnlyList<string> diskKeys)
    {
        var steps = new List<PlannedStep>();
        var order = 1;

        foreach (var diskKey in diskKeys)
        {
            var input = DiskInput(diskKey);
            steps.Add(new PlannedStep(MigrationStepKind.ExportDisk, order++, input));
            steps.Add(new PlannedStep(MigrationStepKind.ConvertDisk, order++, input));
            steps.Add(new PlannedStep(MigrationStepKind.StageArtifact, order++, input));
        }

        steps.Add(new PlannedStep(MigrationStepKind.ProvisionTargetVm, order++, EmptyInput));

        foreach (var diskKey in diskKeys)
            steps.Add(new PlannedStep(MigrationStepKind.AttachDisk, order++, DiskInput(diskKey)));

        steps.Add(new PlannedStep(MigrationStepKind.ConfigureTargetVm, order++, EmptyInput));
        steps.Add(new PlannedStep(MigrationStepKind.VerifyTargetVm, order++, EmptyInput));
        steps.Add(new PlannedStep(MigrationStepKind.Cleanup, order, EmptyInput));
        return steps;
    }

    private static List<PlannedStep> BuildIncrementalSteps()
    {
        return
        [
            new PlannedStep(MigrationStepKind.EnableCbt, 1, EmptyInput),
            new PlannedStep(MigrationStepKind.IncrementalPull, 2, EmptyInput),
            new PlannedStep(MigrationStepKind.ApplyDelta, 3, EmptyInput),
            new PlannedStep(MigrationStepKind.FinalSyncCutover, 4, EmptyInput),
            new PlannedStep(MigrationStepKind.VerifyTargetVm, 5, EmptyInput)
        ];
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyInput =
        new Dictionary<string, string>();

    private static Dictionary<string, string> DiskInput(string diskKey) =>
        new() { ["diskKey"] = diskKey };
}
