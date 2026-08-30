using VMTO.Domain.Aggregates.Connection;

namespace VMTO.Domain.ValueObjects;

public sealed record PlatformCapabilities(
    PlatformKind Platform,
    bool CanBeSource,
    bool CanBeTarget,
    bool SupportsOfflineExport,
    bool SupportsIncrementalExport,
    bool SupportsOnlineExport);

public static class PlatformCapabilityCatalog
{
    public static PlatformCapabilities For(PlatformKind platform) => platform switch
    {
        PlatformKind.VSphere => new(
            PlatformKind.VSphere,
            CanBeSource: true,
            CanBeTarget: false,
            SupportsOfflineExport: true,
            SupportsIncrementalExport: true,
            SupportsOnlineExport: false),
        PlatformKind.HyperV => new(
            PlatformKind.HyperV,
            CanBeSource: true,
            CanBeTarget: false,
            SupportsOfflineExport: true,
            SupportsIncrementalExport: false,
            SupportsOnlineExport: false),
        PlatformKind.ProxmoxVE => new(
            PlatformKind.ProxmoxVE,
            CanBeSource: false,
            CanBeTarget: true,
            SupportsOfflineExport: false,
            SupportsIncrementalExport: false,
            SupportsOnlineExport: false),
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown platform.")
    };
}
