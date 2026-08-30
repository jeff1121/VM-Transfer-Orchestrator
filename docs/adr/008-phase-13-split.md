# Phase 13 splits into architecture (13a) and Hyper-V host MVP (13b)

Phase 13 stays bound to `plan.md`, but "done" is two increments. 13a realigns the generic source/target model and plan-driven saga, keeps Hyper-V on mocks, and restores vSphere. 13b is the real Windows transport, export, conversion, boot verification, and rollback.

**Status**: accepted

**Considered Options**
- One phase until all six plan acceptance criteria pass.
- Split 13a / 13b (chosen).
- Hotfix the saga only and freeze Hyper-V.

**Consequences**
- README and Tasks.md must describe 13a as in progress and must not call the Hyper-V MVP complete.
- plan.md acceptance criteria 3–5 move to 13b; criteria 6 (vSphere must still pass) is a 13a exit gate.
- Connection metadata for Hyper-V transport is designed in 13a even if the agent is not built yet.
