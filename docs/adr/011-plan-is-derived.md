# Operators choose Source, Target, and Options; the plan is derived

Create Job does not take a named strategy. `HyperVOffline` is not a domain concept. Full copy versus incremental is a Migration Option gated by Source capabilities. The Migration Plan is built and snapshotted on the job.

**Status**: accepted

**Considered Options**
- Derive the plan from Source, Target, disks, and options (chosen).
- Keep `FullCopy` / `Incremental` as the only operator-facing strategies.
- Keep `HyperVOffline` as a third strategy.

**Consequences**
- Drop `MigrationStrategy.HyperVOffline` from the ubiquitous language and from the create-job UI.
- A persisted full-copy versus incremental label on the job, if kept, is derived for filters and metrics — it is not an input.
- `MigrationPlanBuilder` becomes the 13a source of step sequences; the saga must follow the snapshot, not a strategy switch.
