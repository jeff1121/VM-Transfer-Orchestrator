using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace VMTO.Infrastructure.Telemetry.HealthChecks;

public sealed class RabbitMqQueueHealthCheck(
    IConfiguration configuration,
    IConnection connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            var queues = configuration.GetSection("HealthChecks:RabbitMQ:Queues").Get<string[]>() ?? [];
            foreach (var queue in queues)
            {
                await channel.QueueDeclarePassiveAsync(queue, cancellationToken);
            }

            return HealthCheckResult.Healthy("RabbitMQ connection and configured queues are healthy.");
        }
        catch (OperationInterruptedException ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ queue check failed.", ex);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ check failed.", ex);
        }
    }
}
