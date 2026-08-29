# VMTO Multi-Platform Migration Plan

## Purpose

Extend VMTO from the current **vSphere → Proxmox VE** flow to a platform-extensible migration product. The first production path is **Hyper-V → Proxmox VE**. The refactor must preserve the existing vSphere flow and make later adapters (KVM/libvirt, OpenStack, Nutanix AHV) additive rather than requiring a new Saga per platform pair.

## Product boundaries

- **In scope**: Hyper-V source discovery, offline export, VHD/VHDX conversion, transfer through existing storage, Proxmox provisioning, validation, UI/API support, observability, and automated tests.
- **Out of scope for 13a / 13b / 14**: live migration, Hyper-V checkpoint-chain migration, Hyper-V Replica, incremental replication, automatic Windows guest driver remediation, new target platforms, Failover Cluster / CSV / SCVMM, WinRM transport, a third source platform (KVM / OpenStack / AHV), and deleting or modifying the Source VM.
- **Safety rule**: the MVP migrates only a powered-off VM with no active checkpoint chain. The source is never deleted or modified by VMTO. `DeleteSourceAfter` is not an option.
- **Connection**: bound to one **Platform**. Hyper-V Transport is an mTLS HTTPS Windows agent on **one standalone host**. Transport is a Connection setting, not a new Platform.
- **Disks**: one job migrates one Eligible VM and every supported disk. No subset picker. An unsupported disk makes the VM ineligible.
- **Licensing**: Air-gapped offline 16-character key (`XXXX-XXXX-XXXX-XXXX`, Crockford Base32 + truncated 48-bit HMAC-SHA256). Gated on max concurrent jobs and platform/feature flags. No internet connection required.
- **Failure**: all-or-nothing on the Target. Rollback deletes the Target VM this job created. Export Cleanup may remove temporary export files only.
- **Success**: every Selected Disk checksum matches and the Target reports the VM running. Guest IP / heartbeat are evidence, not a gate.
- **Create Job**: operators choose Source, Target, and Options. The Migration Plan is derived and snapshotted. There is no `HyperVOffline` strategy.

Language lives in `CONTEXT.md`. Architectural decisions: ADR-006 through ADR-019.

## Delivery increments (locked 2026-08-28)

| Increment | Outcome | Exit criteria |
| --- | --- | --- |
| **13a Architecture** | Generic Source/Target ports, `PlatformKind`, `MigrationPlanBuilder`, plan-driven saga, rollback/cleanup ports, honest docs. Hyper-V stays mock. | Existing vSphere → PVE mock path passes. Saga is not hardcoded to `ExportVhdx`. README / Tasks match this plan. |
| **13b Hyper-V host MVP** | Windows mTLS agent on a standalone host exports all supported disks, converts, provisions PVE, starts the VM, and rolls back on failure. | A representative multi-disk Windows and Linux powered-off VM can reach checksum + running. Clustered / checkpoint / pass-through VMs are rejected with guidance. |
| **14 Real dual-path pilot** | Run vSphere and Hyper-V against real hosts. Finish mapping evidence and operator runbooks. | Staged non-production pilot evidence exists. No third Platform. |

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

Sprints 0–1 are **13a**. Sprints 2–4 are **13b** except where a UI/API stub is required for 13a (Hyper-V connection type, read-only disk list, mock pre-flight). Sprint 4 dry-run, agent runbooks, and platform metrics complete in 13b. Phase 14 is the real-lab epic after 13b.

| Increment | Outcome | Exit criteria |
| --- | --- | --- |
| 0. Architecture baseline (13a) | New ports, capability model, and ADR; no behavioural regression. | Existing vSphere tests pass using adapters. |
| 1. Generic orchestration (13a) | Saga executes typed plan steps instead of `ExportVmdk`/`ImportToPve` names. | Existing vSphere → PVE end-to-end mock path passes. |
| 2. Hyper-V discovery/export (13b) | A source agent lists eligible VMs and exports a powered-off VM's supported disks to VHD/VHDX. | Export manifest and checksums are persisted. Clustered VMs rejected. |
| 3. Hyper-V → PVE MVP (13b) | VHD/VHDX converts, imports, Target VM is running, checksums match. Guest health is evidence not a gate. | A representative Windows and Linux VM test matrix has evidence. Failure rolls back the Target VM. |
| 4. Product hardening (13b) | UI capability warnings, audit, metrics, dry-run, operator documentation. | Dry-run, retry, cleanup, and rollback evidence is available. |
| 5. Real dual-path pilot (14) | Real vSphere + real Hyper-V. | Mapping table filled from evidence. No new Platform. |

## Iterations

### Sprint 0 — Architecture baseline (13a)

- Publish an ADR describing source/target adapter separation and the Hyper-V offline-only MVP.
- Introduce `PlatformKind`, platform capabilities, neutral VM/disk models, and adapter registry.
- Add contract tests that every adapter must satisfy.
- Keep legacy vSphere and PVE classes behind compatibility adapters while the workflow changes.

### Sprint 1 — Generic migration plan and Saga (13a)

- Replace string-only step definitions with typed `MigrationStepKind` values and per-step input/output contracts.
- Replace `ExportVmdkMessage` and `ImportToPveMessage` with platform-neutral export/provision/attach messages.
- Make the Saga publish its next message from `MigrationPlan`, persist plan version and adapter IDs, and carry structured step outputs.
- Add compensating actions: source cleanup and target rollback for partial provisioning.

### Sprint 2 — Hyper-V source adapter (13b)

- Deliver a Windows-hosted source agent over **mTLS HTTPS**. WinRM is a documented later fallback, not built in 13b. Do not run `Export-VM` inside the Linux worker. One Connection is one standalone host running one agent.
- Implement connection validation, VM listing, pre-flight checks, VM shutdown-state verification, `Export-VM`, manifest collection, and secure artifact streaming.
- Reject checkpoint chains and unsupported dynamic-disk configurations with actionable errors.

### Sprint 3 — Hyper-V → Proxmox MVP (13b)

- Extend conversion to explicitly detect VHD/VHDX and convert to the chosen PVE-compatible output.
- Normalize firmware, CPU, memory, disk-bus, NIC, and network metadata before target provisioning.
- Reuse the Proxmox target adapter after it is moved behind `IVmTargetAdapter`.
- Add test fixtures for Generation 1 BIOS, Generation 2 UEFI, Windows, and Linux guests.

### Sprint 4 — Product readiness (13b; 13a may stub Hyper-V connection UI only)

- Add UI platform selectors, capability warnings, dry-run output, and unsupported-feature guidance.
- Deliver State-Aware commercial licensing UX in Settings: active tier dashboard, expiry countdown, masked key display, and collapsible renewal input to prevent operator confusion.
- Deliver Layout Hierarchy & Interaction Upgrade:
  - **Auth Layout Isolation**: Hide sidebars, topbars, and navigation links on login / unauthenticated routes, showing only the centered glassmorphic card.
  - **Dynamic Mini-Sidebar & Pinning**: Provide 72px compact icon mode and 260px expanded mode with hover-to-expand and persistent pin/lock toggle.
  - **Notification Drawer Click-Outside**: Support backdrop overlay, click-outside dismissal, close button, and `Esc` key dismissal.
- Enforce feature gating and concurrency limit checks in `CreateJobHandler` based on decoded license capabilities.
- Add metrics segmented by source/target platform, audit events, cleanup jobs, structured error classifications, and operator runbooks.
- Complete an interoperability matrix and staged pilot with non-production VMs.

## Acceptance criteria for the MVP

Criteria 6 is a **13a** exit gate. Criteria 1–5 are **13b**.

1. An operator can create, validate, and select a Hyper-V source connection (agent URL + client certificate reference) and a Proxmox target connection.
2. The system lists eligible powered-off Hyper-V VMs on that standalone host without exposing credentials in logs or API responses. Clustered, replica, checkpoint-chain, and pass-through VMs are ineligible.
3. Every supported disk is exported, checksummed, converted, imported, attached; the Target VM is started and reported running.
4. VMTO records the selected adapters, source VM identity, disk metadata, plan version, timing, and final verification result.
5. A failure leaves the source untouched; a partially provisioned target is removed through a recorded compensating action (whole Target VM).
6. Existing vSphere → Proxmox mock and integration paths continue to pass on the plan-driven saga.

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
