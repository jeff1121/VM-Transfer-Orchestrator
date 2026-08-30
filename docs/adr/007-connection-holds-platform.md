# A Connection holds a Platform; transport is a setting

A Connection is bound to one Platform (vSphere, Hyper-V, Proxmox VE). Hyper-V-via-agent and Hyper-V-via-WinRM are two Connections of the same Platform, not two Platforms.

**Status**: accepted

**Considered Options**
- Platform on Connection; transport in connection settings (chosen).
- Flatten transports into types (`HyperVAgent`, `HyperVWinRM`).
- One global Hyper-V transport for all Connections.

**Consequences**
- `ConnectionType` in code is the Platform concept; it should become `PlatformKind` (plan.md) rather than grow agent/WinRM values.
- Adapter registration keys off Platform, not transport.
- Secrets stay in the encrypted secret field; transport settings may live in non-secret connection metadata.
