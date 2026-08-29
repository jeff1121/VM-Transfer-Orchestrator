# VM Transfer Orchestrator

VMTO migrates virtual machines from a source platform to a target platform. A platform is not assumed to work in both directions.

## Language

**Source**:
The platform a virtual machine is exported from.
_Avoid_: origin, from-host

**Target**:
The platform a virtual machine is provisioned onto.
_Avoid_: destination, to-host, PVE (PVE is one target, not the concept)

**Migration Job**:
One orchestrated attempt to move a selected VM from a source to a target.
_Avoid_: task, transfer, ticket

**Migration Plan**:
The ordered, typed steps for a job. It is derived from Source capabilities, Target capabilities, the Eligible VM, selected disks, and Migration Options, then snapshotted when the job is created so a retry does not pick up later setting changes.
_Avoid_: workflow, pipeline, strategy (operators do not choose a named strategy)

**Migration Options**:
Operator-chosen settings that are not identity: target disk format, checksum verification, whether incremental is requested, dry-run. Incremental may be requested only when the Source capability allows it. Hyper-V does not allow incremental in the MVP. Deleting or modifying the Source is not an option.
_Avoid_: strategy, HyperVOffline, DeleteSourceAfter

**Migration Step**:
One typed unit of work in a plan, such as exporting a disk or provisioning the target VM. Steps are platform-neutral; they are not named after a disk format or a hypervisor.
_Avoid_: ExportVmdk, ExportVhdx, ImportToPve as domain names

**Offline Export**:
An export allowed only when the VM is powered off and has no checkpoint chain. The source VM is never deleted or modified.
_Avoid_: live migration, clone

**Artifact**:
A disk image, plus its checksum and manifest, stored while the job runs.
_Avoid_: file, image, blob

**Platform**:
A hypervisor family such as vSphere, Hyper-V, or Proxmox VE. A Connection is bound to exactly one Platform. Whether that Platform can act as Source, Target, or both is a capability, not its identity.
_Avoid_: ConnectionType, hypervisor type, vendor

**Connection**:
An operator-saved, credentialed endpoint bound to one Platform. How the Connection reaches that Platform (for example a Windows agent or WinRM) is a setting on the Connection, not a different Platform.
_Avoid_: account, credential, host

**Transport**:
The method a Connection uses to reach its Platform. Transport is not a Platform and not a Migration Step. The Hyper-V MVP transport is a Windows source agent reached over mTLS HTTPS. WinRM is a later fallback, not a second Platform.
_Avoid_: protocol, ConnectionType

**Eligible VM**:
A VM the Source is allowed to export. For Hyper-V this is a powered-off VM on that standalone host, with no checkpoint chain, no pass-through disk, and not a clustered role.
_Avoid_: any VM, guest, replica

**Selected Disk**:
Every supported virtual disk on the Eligible VM. The operator does not pick a subset. If the VM has an unsupported disk, the VM is not eligible.
_Avoid_: volume, LUN, drive letter as the identity of the disk, optional data disk


**Target Rollback**:
The recorded compensating action that removes Target resources this job created, after any failure. A job is all-or-nothing on the Target: leftover disks do not stay attached to a half-migrated VM.
_Avoid_: undo, partial success, leave-for-operator

**Export Cleanup**:
Removal of temporary export files produced on the Source host or agent. It is not a change to the Source VM.
_Avoid_: compensation as a synonym, delete source

**Verification**:
The last Migration Step that decides whether the job Succeeded. It passes when every Selected Disk checksum matches and the Target reports the VM running. Guest IP, heartbeat, and NIC checks are evidence for a test matrix, not a success gate, and they do not trigger Target Rollback.
_Avoid_: guest-agent health as the definition of success, boot test placeholder

