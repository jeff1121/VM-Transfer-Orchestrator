# A Hyper-V Connection is one standalone host

One Hyper-V Connection is one standalone host running one agent. Failover Cluster, CSV, SCVMM, and Hyper-V Replica are out of scope for 13b.

**Status**: accepted

**Considered Options**
- One host, one agent, cluster out of scope (chosen).
- One Connection is a Failover Cluster with per-node agents.
- One Connection is SCVMM.

**Consequences**
- Pre-flight must reject clustered, CSV, replica, checkpoint-chain, and pass-through disks.
- 13a metadata stays a single agent base URL per Connection.
- Cluster support later is more Connection settings on the same Hyper-V Platform, not a new Platform.
