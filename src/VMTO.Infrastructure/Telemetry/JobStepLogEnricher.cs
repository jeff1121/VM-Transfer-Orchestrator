using Serilog.Core;
using Serilog.Events;

namespace VMTO.Infrastructure.Telemetry;

public sealed class JobStepLogEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (!string.IsNullOrWhiteSpace(JobStepLogContext.CurrentJobId))
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("JobId", JobStepLogContext.CurrentJobId));
        }

        if (!string.IsNullOrWhiteSpace(JobStepLogContext.CurrentStepId))
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("StepId", JobStepLogContext.CurrentStepId));
        }
    }
}
