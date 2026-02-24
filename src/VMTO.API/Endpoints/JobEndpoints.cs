using VMTO.Application.DTOs;
using VMTO.Application.Ports.Repositories;
using VMTO.Domain.Aggregates.MigrationJob;
using VMTO.Domain.Enums;

namespace VMTO.API.Endpoints;

public static class JobEndpoints
{
    public static void MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs").WithTags("Jobs");

        group.MapGet("/", ListJobs);
        group.MapGet("/{id:guid}", GetJob);
        group.MapPost("/", CreateJob);
        group.MapPost("/{id:guid}/cancel", CancelJob);
        group.MapPost("/{id:guid}/pause", PauseJob);
        group.MapPost("/{id:guid}/resume", ResumeJob);
        group.MapPost("/{id:guid}/retry", RetryFailedSteps);
        group.MapGet("/{id:guid}/progress", GetJobProgress);
    }

    private static async Task<IResult> ListJobs(
        IJobRepository repo,
        int page = 1,
        int pageSize = 20,
        JobStatus? status = null,
        CancellationToken ct = default)
    {
        var jobs = await repo.ListAsync(page, pageSize, status, ct);
        var total = await repo.CountAsync(status, ct);
        return Results.Ok(new { items = jobs.Select(MapToDto), total, page, pageSize });
    }

    private static async Task<IResult> GetJob(Guid id, IJobRepository repo, CancellationToken ct)
    {
        var job = await repo.GetByIdAsync(id, ct);
        if (job is null) return Results.NotFound();
        return Results.Ok(MapToDto(job));
    }

    private static async Task<IResult> CreateJob(
        CreateJobRequest request,
        IJobRepository repo,
        CancellationToken ct)
    {
        var job = new MigrationJob(
            request.SourceConnectionId,
            request.TargetConnectionId,
            request.StorageTarget,
            request.Strategy,
            request.Options);

        await repo.AddAsync(job, ct);
        return Results.Created($"/api/jobs/{job.Id}", MapToDto(job));
    }

    private static async Task<IResult> CancelJob(Guid id, IJobRepository repo, CancellationToken ct)
    {
        var job = await repo.GetByIdAsync(id, ct);
        if (job is null) return Results.NotFound();
        job.RequestCancel();
        await repo.UpdateAsync(job, ct);
        return Results.Ok(MapToDto(job));
    }

    private static async Task<IResult> PauseJob(Guid id, IJobRepository repo, CancellationToken ct)
    {
        var job = await repo.GetByIdAsync(id, ct);
        if (job is null) return Results.NotFound();
        job.RequestPause();
        await repo.UpdateAsync(job, ct);
        return Results.Ok(MapToDto(job));
    }

    private static async Task<IResult> ResumeJob(Guid id, IJobRepository repo, CancellationToken ct)
    {
        var job = await repo.GetByIdAsync(id, ct);
        if (job is null) return Results.NotFound();
        job.RequestResume();
        await repo.UpdateAsync(job, ct);
        return Results.Ok(MapToDto(job));
    }

    private static async Task<IResult> RetryFailedSteps(Guid id, IJobRepository repo, CancellationToken ct)
    {
        var job = await repo.GetByIdAsync(id, ct);
        if (job is null) return Results.NotFound();
        foreach (var step in job.Steps.Where(s => s.Status == StepStatus.Failed))
        {
            step.Retry();
        }
        await repo.UpdateAsync(job, ct);
        return Results.Ok(MapToDto(job));
    }

    private static async Task<IResult> GetJobProgress(Guid id, IJobRepository repo, CancellationToken ct)
    {
        var job = await repo.GetByIdAsync(id, ct);
        if (job is null) return Results.NotFound();
        var progress = new JobProgressDto(
            job.Id,
            job.Status,
            job.Progress,
            job.Steps.Select(MapStepToDto).ToList());
        return Results.Ok(progress);
    }

    private static JobDto MapToDto(MigrationJob job) =>
        new(job.Id,
            job.CorrelationId.Value,
            job.Strategy,
            job.Status,
            job.Progress,
            job.CreatedAt,
            job.UpdatedAt,
            job.Steps.Select(MapStepToDto).ToList());

    private static JobStepDto MapStepToDto(JobStep step) =>
        new(step.Id,
            step.Name,
            step.Order,
            step.Status,
            step.Progress,
            step.RetryCount,
            step.ErrorMessage);
}

public sealed record CreateJobRequest(
    Guid SourceConnectionId,
    Guid TargetConnectionId,
    Domain.ValueObjects.StorageTarget StorageTarget,
    MigrationStrategy Strategy,
    MigrationOptions Options);
