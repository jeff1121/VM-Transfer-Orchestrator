# One job migrates one VM and every selected disk

A Migration Job is per VM. 13b exports, converts, and attaches each Selected Disk in that job. The plan is a per-disk step list plus one configure/verify tail. Single-disk-only was rejected.

**Status**: accepted

**Considered Options**
- Reject multi-disk VMs in pre-flight.
- Require the operator to pick exactly one disk and leave the rest behind.
- Migrate all selected disks in one job (chosen).

**Consequences**
- 13a `MigrationPlan` must be a list that can repeat export/convert/attach per disk.
- The saga cannot keep a single `DiskKey` field as the real model.
- Partial disk failure needs an explicit compensation rule (next decision).
- Default selection (all disks vs a picker) still has to be specified in the create-job UI.
