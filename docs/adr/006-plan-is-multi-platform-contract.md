# Plan.md is the multi-platform contract

Phase 13 keeps the source/target adapter split and generic Migration Plan in `plan.md`. The current `ISourcePlatformPort` / `ExportVhdx` / `HyperVOffline` path is scaffolding, not the product shape.

**Status**: accepted

**Considered Options**
- Treat `plan.md` as binding and realign the code (chosen).
- Ratify the platform-specific saga messages and rewrite the plan to match.
- Ignore the existing Hyper-V code and pretend Phase 13 has not started.

**Consequences**
- README must not claim Phase 13 is done.
- Tasks.md should show partial progress against the plan, not 0% and not 100%.
- `MigrationJobSaga` must not stay hardcoded to `ExportVhdxMessage`; that breaks vSphere.
