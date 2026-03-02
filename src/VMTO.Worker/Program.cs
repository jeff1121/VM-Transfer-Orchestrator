using Hangfire;
using Hangfire.PostgreSql;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using VMTO.Infrastructure;
using VMTO.Worker;
using VMTO.Worker.Consumers;
using VMTO.Worker.Persistence;
using VMTO.Worker.Sagas;

var builder = Host.CreateApplicationBuilder(args);

// Infrastructure (EF, Redis, clients, crypto, telemetry)
builder.Services.AddInfrastructure(builder.Configuration);

// EF Core DbContext for saga state persistence
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("PostgreSQL")
    ?? "Host=localhost;Database=vmto;Username=vmto;Password=vmto";

builder.Services.AddDbContext<MigrationSagaDbContext>(options =>
    options.UseNpgsql(connectionString));

// MassTransit + RabbitMQ with consumers and saga
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ExportVmdkConsumer>();
    x.AddConsumer<ConvertDiskConsumer>();
    x.AddConsumer<UploadArtifactConsumer>();
    x.AddConsumer<ImportToPveConsumer>();
    x.AddConsumer<VerifyConsumer>();
    x.AddConsumer<EnableCbtConsumer>();
    x.AddConsumer<IncrementalPullConsumer>();
    x.AddConsumer<ApplyDeltaConsumer>();
    x.AddConsumer<FinalSyncCutoverConsumer>();

    x.AddSagaStateMachine<MigrationJobSaga, MigrationJobSagaState>()
     .EntityFrameworkRepository(r =>
     {
         r.ConcurrencyMode = ConcurrencyMode.Optimistic;
         r.ExistingDbContext<MigrationSagaDbContext>();
         r.UsePostgres();
     });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("RabbitMQ") ?? "rabbitmq://localhost");
        cfg.ConfigureEndpoints(context);
    });
});

// Hangfire (for scheduled jobs)
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(connectionString)));
builder.Services.AddHangfireServer();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
