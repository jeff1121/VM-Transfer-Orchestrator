# 13a ships a plan-driven saga

13a replaces platform-specific first messages and hardcoded Convert→Upload→Import→Verify transitions with a saga that publishes the next step from the job's snapshotted Migration Plan. Step kinds are platform-neutral. vSphere FullCopy and mock Hyper-V share that saga. Restoring vSphere by branching on ExportVmdk versus ExportVhdx is rejected.

**Status**: accepted

**Considered Options**
- Plan-driven generic steps in 13a (chosen).
- Only branch the first published message; keep the rest hardcoded.
- Docs and ports in 13a; saga stays a hotfix if/else.

**Consequences**
- `ExportVmdkMessage` / `ExportVhdxMessage` / `ImportToPveMessage` are compatibility leftovers to delete or wrap during 13a, not the model.
- 13a exit includes the existing vSphere mock path passing on the new saga.
- Per-disk step lists and Target Rollback ports land in 13a even though real PVE delete is 13b.
