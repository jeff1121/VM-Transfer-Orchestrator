# Success is checksum plus Target VM running

Verification succeeds when every Selected Disk checksum matches and the Target reports the VM running. Guest IP, heartbeat, and NIC connectivity are 13b evidence, not an automated gate. Missing guest tools must not roll back a running VM.

**Status**: accepted

**Considered Options**
- Checksum + Target running as the gate (chosen).
- Require guest heartbeat/IP as the gate.
- Checksum only; boot is a manual runbook.

**Consequences**
- 13b Verify must actually start the Target VM, not only hash artifacts.
- Driver injection stays out of scope; first-boot Windows may be running but unhealthy.
- Target Rollback is for migration-step failure, not for a running VM that lacks a guest agent.
