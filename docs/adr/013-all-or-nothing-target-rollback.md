# A failed job rolls back the whole Target VM

Any Selected Disk failure fails the job. Compensation deletes the Target VM this job provisioned, including disks already attached, and then runs Export Cleanup. The Source VM is not modified. Retry starts from the snapshotted plan.

**Status**: accepted

**Considered Options**
- All-or-nothing Target rollback (chosen).
- Leave a partial Target VM and mark the job failed/partial.
- Retry only the failed disk against the existing Target VM.

**Consequences**
- 13a must define rollback and idempotency on the Target port, even if 13b is the first real PVE delete.
- Progress UI must not present a failed multi-disk job as a usable VM.
- A later "repair disk only" flow would be a new command, not a silent change to this rule.
