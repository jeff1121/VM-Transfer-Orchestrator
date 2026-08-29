# Hyper-V transport is an mTLS HTTPS Windows agent

13b talks to Hyper-V through a Windows source agent on the host, over mTLS HTTPS. The Linux worker does not run `Export-VM`, does not speak WinRM, and does not store domain credentials for Hyper-V.

**Status**: accepted

**Considered Options**
- mTLS HTTPS agent only for 13b (chosen).
- Constrained WinRM/PowerShell only.
- Ship agent and WinRM together in 13b.

**Consequences**
- 13a Connection settings for Hyper-V reserve agent base URL and client certificate reference; secrets stay in the encrypted secret field.
- Existing `HyperVClient` HTTP shape can be kept and pointed at the Connection's agent URL.
- WinRM remains a documented fallback for a later increment if a host cannot install the agent.
