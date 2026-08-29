# The Source is never deleted or modified

`DeleteSourceAfter` is not a Migration Option. A Migration Job must not delete, convert-in-place, or otherwise change the Source VM. Clearing the Source after cutover is a separate future command, not part of 13a or 13b.

**Status**: accepted

**Considered Options**
- Remove the option entirely (chosen).
- Keep the field and force it false.
- Add a separate post-cutover decommission command in this phase.

**Consequences**
- 13a removes `DeleteSourceAfter` from the domain, API, OpenAPI, and New Job UI.
- The Hyper-V agent must not be granted delete-VM rights for the MVP.
- Export Cleanup may delete temporary export files only, never the Source VM or its original VHD/VHDX.
