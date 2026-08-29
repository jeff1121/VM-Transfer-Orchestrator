# After 13b, the next epic is a real dual-path pilot

Phase 14 does not add a third Platform. It runs the vSphere and Hyper-V paths against real hosts, finishes mapping evidence and operator runbooks, and proves the 13a skeleton in an unclean environment.

**Status**: accepted

**Considered Options**
- Real-lab hardening of the two existing paths (chosen).
- Next source Platform (KVM/libvirt).
- Deepen Hyper-V with cluster, incremental, WinRM, or live migration.

**Consequences**
- KVM, OpenStack, and AHV stay "additive later", not the next backlog item.
- Cluster, Hyper-V incremental, WinRM, and live migration stay explicit no until a later epic.
- 13a/13b documentation should point at Phase 14 as evidence-gathering, not as more architecture.
