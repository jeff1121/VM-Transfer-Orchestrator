using Serilog.Core;
using Serilog.Events;

namespace VMTO.Infrastructure.Telemetry;

public sealed class SensitiveDataMaskingEnricher : ILogEventEnricher
{
    private static readonly string[] SensitiveKeys = ["password", "secret", "token"];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var propertyName in logEvent.Properties.Keys.ToArray())
        {
            if (!IsSensitive(propertyName))
            {
                continue;
            }

            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(propertyName, "***"));
        }
    }

    private static bool IsSensitive(string propertyName)
    {
        foreach (var key in SensitiveKeys)
        {
            if (propertyName.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
