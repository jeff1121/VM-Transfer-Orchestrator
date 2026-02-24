using System.Diagnostics;
using System.Reflection;

namespace VMTO.Shared.Telemetry;

public static class ActivitySources
{
    public const string Name = "VMTO";
    public static readonly string Version =
        typeof(ActivitySources).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.1.0";
    public static readonly ActivitySource Default = new(Name, Version);
}
