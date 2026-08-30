# A job migrates every supported disk; there is no subset picker

13b has no disk picker. Every supported disk on the Eligible VM is in the job. An unsupported disk (pass-through, checkpoint chain, unknown format) makes the whole VM ineligible.

**Status**: accepted

**Considered Options**
- No picker; unsupported disk fails the VM (chosen).
- Default-all with optional deselect.
- Mandatory picker with no default.

**Consequences**
- Create Job UI lists disks as read-only facts, not checkboxes.
- Pre-flight must classify each disk as supported or not.
- A later "leave this disk behind" flow needs an explicit confirmation command; it is not a checkbox on MVP.
