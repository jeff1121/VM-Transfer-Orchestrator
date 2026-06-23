using Microsoft.Extensions.DependencyInjection;
using VMTO.Application.Commands;
using VMTO.Application.Commands.Connections;
using VMTO.Application.Commands.Handlers;
using VMTO.Application.Commands.Jobs;
using VMTO.Application.DTOs;
using VMTO.Application.Queries;
using VMTO.Application.Queries.Artifacts;
using VMTO.Application.Queries.Connections;
using VMTO.Application.Queries.Handlers;
using VMTO.Application.Queries.Jobs;

namespace VMTO.Application;

/// <summary>
/// Application 層的相依性注入註冊擴充方法。
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // 註冊 Command Handlers
        services.AddScoped<ICommandHandler<CreateJobCommand, Guid>, CreateJobHandler>();
        services.AddScoped<ICommandHandler<CancelJobCommand>, CancelJobHandler>();
        services.AddScoped<ICommandHandler<PauseJobCommand>, PauseJobHandler>();
        services.AddScoped<ICommandHandler<ResumeJobCommand>, ResumeJobHandler>();
        services.AddScoped<ICommandHandler<RetryFailedStepsCommand>, RetryFailedStepsHandler>();
        services.AddScoped<ICommandHandler<CreateConnectionCommand, Guid>, CreateConnectionHandler>();
        services.AddScoped<ICommandHandler<ValidateConnectionCommand>, ValidateConnectionHandler>();
        services.AddScoped<ICommandHandler<DeleteConnectionCommand>, DeleteConnectionHandler>();

        // 註冊 Query Handlers
        services.AddScoped<IQueryHandler<GetJobQuery, JobDto>, GetJobHandler>();
        services.AddScoped<IQueryHandler<ListJobsQuery, IReadOnlyList<JobDto>>, ListJobsHandler>();
        services.AddScoped<IQueryHandler<GetJobProgressQuery, JobProgressDto>, GetJobProgressHandler>();
        services.AddScoped<IQueryHandler<GetConnectionsQuery, IReadOnlyList<ConnectionDto>>, GetConnectionsHandler>();
        services.AddScoped<IQueryHandler<GetArtifactsQuery, IReadOnlyList<ArtifactDto>>, GetArtifactsHandler>();

        return services;
    }
}
