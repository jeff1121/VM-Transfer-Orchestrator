using System.Diagnostics;

namespace VMTO.Shared.Telemetry;

public static class ActivitySources
{
    public const string Name = "VMTO";
    public static readonly ActivitySource Default = new(Name);
}
