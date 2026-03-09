using Hangfire;
using Hangfire.PostgreSql;
using MassTransit;
using VMTO.Infrastructure;
using VMTO.Infrastructure.Resilience;
using VMTO.Worker;
using VMTO.Worker.Consumers;
using VMTO.Worker.Sagas;

var builder = Host.CreateApplicationBuilder(args);

// Infrastructure (EF, Redis, clients, crypto, telemetry)
builder.Services.AddInfrastructure(builder.Configuration);

// MassTransit + RabbitMQ with consumers and saga
builder.Services.AddMassTransit(x =>
{
    var retryOptions = builder.Configuration.GetSection("Resilience:Retry").Get<RetryPolicyOptions>() ?? new RetryPolicyOptions();

    x.AddConsumer<ExportVmdkConsumer>();
    x.AddConsumer<ConvertDiskConsumer>();
    x.AddConsumer<UploadArtifactConsumer>();
    x.AddConsumer<ImportToPveConsumer>();
    x.AddConsumer<VerifyConsumer>();
    x.AddConsumer<DlqConsumer>();

    x.AddSagaStateMachine<MigrationJobSaga, MigrationJobSagaState>()
     .InMemoryRepository();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("RabbitMQ") ?? "rabbitmq://localhost");
        cfg.UseMessageRetry(r => r.Exponential(
            retryLimit: retryOptions.MaxRetryAttempts,
            minInterval: TimeSpan.FromSeconds(retryOptions.BaseDelaySeconds),
            maxInterval: TimeSpan.FromSeconds(retryOptions.MaxDelaySeconds),
            intervalDelta: TimeSpan.FromSeconds(retryOptions.BaseDelaySeconds)));
        cfg.ConfigureEndpoints(context);
    });
});
builder.Services.AddOptions<MassTransitHostOptions>().Configure(options =>
{
    options.WaitUntilStarted = true;
    options.StartTimeout = TimeSpan.FromSeconds(30);
    options.StopTimeout = TimeSpan.FromSeconds(60);
});
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(90);
});

// Hangfire (for scheduled jobs)
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));
builder.Services.AddHangfireServer();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
