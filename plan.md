# VMTO Multi-Platform Migration Plan

## Purpose

Extend VMTO from the current **vSphere → Proxmox VE** flow to a platform-extensible migration product. The first production path is **Hyper-V → Proxmox VE**. The refactor must preserve the existing vSphere flow and make later adapters (KVM/libvirt, OpenStack, Nutanix AHV) additive rather than requiring a new Saga per platform pair.

## Product boundaries

- **In scope**: Hyper-V source discovery, offline export, VHD/VHDX conversion, transfer through existing storage, Proxmox provisioning, validation, UI/API support, observability, and automated tests.
- **Out of scope for the first release**: live migration, Hyper-V checkpoint-chain migration, Hyper-V Replica, incremental replication, automatic Windows guest driver remediation, and new target platforms.
- **Safety rule**: the MVP migrates only a powered-off VM with no active checkpoint chain. The source is never deleted or modified by VMTO.

## Target architecture

Separate source and target responsibilities. A platform is not assumed to support both directions.

```text
Connection + capabilities
        |
        v
IVmSourceAdapter ------> MigrationPlan ------> IVmTargetAdapter
  list / inspect              |                 validate / provision
  prepare / export            v                 attach / configure / verify
                         Generic Saga
                         ExportDisk
                         ConvertDisk
                         StageArtifact
                         ProvisionTargetVm
                         AttachDisk
                         ConfigureTargetVm
                         VerifyTargetVm
```

### Core contracts

| Contract | Responsibility |
| --- | --- |
| `IVmSourceAdapter` | Validate connection, discover VMs/disks, inspect hardware, prepare/export a disk, and cleanup an export session. |
| `IVmTargetAdapter` | Validate connection/capacity, provision a VM, attach artifacts, apply hardware/network settings, start/verify, and rollback partial provisioning. |
| `IPlatformAdapterRegistry` | Resolve an adapter by `PlatformKind`; startup validation prevents missing registrations. |
| `MigrationPlanBuilder` | Produces typed steps based on source capabilities, target capabilities, selected disks, and requested options. |
| `VmHardwareSpec` / `DiskDescriptor` / `NetworkSpec` | Platform-neutral metadata passed through the workflow. |

`ConnectionType` becomes `PlatformKind`, initially `VSphere`, `HyperV`, and `ProxmoxVE`. A capability document records disk formats, firmware, secure-boot support, online-export support, incremental-export support, and network-model support.

## Delivery roadmap

| Increment | Outcome | Exit criteria |
| --- | --- | --- |
| 0. Architecture baseline | New ports, capability model, and ADR; no behavioural regression. | Existing vSphere tests pass using adapters. |
| 1. Generic orchestration | Saga executes typed plan steps instead of `ExportVmdk`/`ImportToPve` names. | Existing vSphere → PVE end-to-end mock path passes. |
| 2. Hyper-V discovery/export | A source agent lists eligible VMs and exports a powered-off VM to VHD/VHDX. | Export manifest and checksums are persisted. |
| 3. Hyper-V → PVE MVP | VHD/VHDX converts, imports, boots, and validates on PVE. | A representative Windows and Linux VM test matrix passes. |
| 4. Product hardening | UI, audit, metrics, fault handling, and operator documentation. | Dry-run, retry, cleanup, and rollback evidence is available. |

## Iterations

### Sprint 0 — Architecture baseline

- Publish an ADR describing source/target adapter separation and the Hyper-V offline-only MVP.
- Introduce `PlatformKind`, platform capabilities, neutral VM/disk models, and adapter registry.
- Add contract tests that every adapter must satisfy.
- Keep legacy vSphere and PVE classes behind compatibility adapters while the workflow changes.

### Sprint 1 — Generic migration plan and Saga

- Replace string-only step definitions with typed `MigrationStepKind` values and per-step input/output contracts.
- Replace `ExportVmdkMessage` and `ImportToPveMessage` with platform-neutral export/provision/attach messages.
- Make the Saga publish its next message from `MigrationPlan`, persist plan version and adapter IDs, and carry structured step outputs.
- Add compensating actions: source cleanup and target rollback for partial provisioning.

### Sprint 2 — Hyper-V source adapter

- Deliver a Windows-hosted source agent using constrained PowerShell/WinRM or a secured local agent API. Do not run `Export-VM` inside the Linux worker.
- Implement connection validation, VM listing, pre-flight checks, VM shutdown-state verification, `Export-VM`, manifest collection, and secure artifact streaming.
- Reject checkpoint chains and unsupported dynamic-disk configurations with actionable errors.

### Sprint 3 — Hyper-V → Proxmox MVP

- Extend conversion to explicitly detect VHD/VHDX and convert to the chosen PVE-compatible output.
- Normalize firmware, CPU, memory, disk-bus, NIC, and network metadata before target provisioning.
- Reuse the Proxmox target adapter after it is moved behind `IVmTargetAdapter`.
- Add test fixtures for Generation 1 BIOS, Generation 2 UEFI, Windows, and Linux guests.

### Sprint 4 — Product readiness

- Add UI platform selectors, capability warnings, dry-run output, and unsupported-feature guidance.
- Add metrics segmented by source/target platform, audit events, cleanup jobs, structured error classifications, and operator runbooks.
- Complete an interoperability matrix and staged pilot with non-production VMs.

## Acceptance criteria for the MVP

1. An operator can create, validate, and select a Hyper-V source connection and a Proxmox target connection.
2. The system lists eligible powered-off Hyper-V VMs without exposing credentials in logs or API responses.
3. A selected VHD/VHDX is exported, checksummed, converted, imported, attached, and booted on PVE.
4. VMTO records the selected adapters, source VM identity, disk metadata, plan version, timing, and final verification result.
5. A failure leaves the source untouched; a partially provisioned target can be removed through a recorded compensating action.
6. Existing vSphere → Proxmox mock and integration paths continue to pass.

## Agile Board mapping

Create one Epic named **Multi-Platform Migration Foundation**. The Phase 13 items in `Tasks.md` map as follows:

- each `13-F*` entry is a Feature;
- each `13-US*` entry is a User Story;
- indented unchecked items are implementation Tasks;
- use tags `platform-extensibility`, `hyper-v`, `proxmox`, `architecture`, and the relevant sprint;
- do not use GitHub Issues for this backlog. Branch, commit, and PR descriptions reference the Azure Boards item with `AB#<id>`.

## Key risks and decisions

| Risk | Mitigation |
| --- | --- |
| Remote Hyper-V export needs Windows privileges | Use a least-privilege Windows source agent and constrained endpoint; do not embed domain credentials in the Linux worker. |
| Checkpoint chains and virtual switches are not portable by default | MVP rejects unsupported chains and produces pre-flight guidance. |
| Guest boot differences (BIOS/UEFI, Secure Boot, drivers) | Persist firmware metadata, make target mapping explicit, and test Windows/Linux representative guests. |
| Saga refactor could regress vSphere | Preserve current clients as adapters and require contract, unit, and mock end-to-end tests before switching defaults. |
| Long exports fail midway | Use resumable storage uploads, manifests/checksums, idempotency keys, and explicit cleanup/retry policies. |
