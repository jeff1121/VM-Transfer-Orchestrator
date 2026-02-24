using Hangfire;
using Hangfire.PostgreSql;
using MassTransit;
using VMTO.Infrastructure;
using VMTO.Worker;
using VMTO.Worker.Consumers;
using VMTO.Worker.Sagas;

var builder = Host.CreateApplicationBuilder(args);

// Infrastructure (EF, Redis, clients, crypto, telemetry)
builder.Services.AddInfrastructure(builder.Configuration);

// MassTransit + RabbitMQ with consumers and saga
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ExportVmdkConsumer>();
    x.AddConsumer<ConvertDiskConsumer>();
    x.AddConsumer<UploadArtifactConsumer>();
    x.AddConsumer<ImportToPveConsumer>();
    x.AddConsumer<VerifyConsumer>();

    x.AddSagaStateMachine<MigrationJobSaga, MigrationJobSagaState>()
     .InMemoryRepository();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("RabbitMQ") ?? "rabbitmq://localhost");
        cfg.ConfigureEndpoints(context);
    });
});

// Hangfire (for scheduled jobs)
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));
builder.Services.AddHangfireServer();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
