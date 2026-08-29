# Offline 16-Character Product Key Licensing Architecture

VMTO adopts a 16-character alphanumeric offline product key (`XXXX-XXXX-XXXX-XXXX`) using Crockford Base32 and truncated HMAC-SHA256 verification. This replaces online activation and heavy license file uploads for air-gapped enterprise environments.

**Status**: accepted

**Context**
- Enterprise virtualization migrations often run in isolated, air-gapped data centers without external internet access.
- Moving files (like `.lic` files) through jump boxes or bastions introduces operational friction and human error.
- Operators need a simple, low-friction mechanism: pasting a 16-character product key directly in the UI.

**Decision**
1. **80-Bit Binary Payload**:
   - 32-bit Payload: 4-bit Version, 3-bit Plan Tier, 5-bit Max Concurrent Jobs (1–32), 12-bit Expiration (months from epoch), 8-bit Feature flags (vSphere, Hyper-V, CBT, Ops).
   - 48-bit Checksum: Truncated HMAC-SHA256 over the 32-bit payload using an internal master key.
2. **Crockford Base32 Encoding**:
   - 80 bits encoded into 16 characters using safe alphabet `23456789ABCDEFGHJKLMNPQRSTUVWXYZ` (excluding confusing characters 0, 1, I, O).
   - Formatted as `XXXX-XXXX-XXXX-XXXX`.
3. **Dual Role**:
   - **Generation Tool** (`VMTO.LicenseServer` / CLI): Issues keys by packing payload and computing HMAC.
   - **Application Validator** (`VMTO.Infrastructure`): Decodes Base32, verifies the 48-bit HMAC signature in constant time, unpacks features/expiration, and stores the active license in `AppDbContext`.

**Consequences**
- Complete offline operation with zero internet dependency.
- Anti-tamper protection with $1 / 2^{48}$ brute-force resistance.
- Gating on job concurrency and feature support in `CreateJobHandler`.
- Smooth UI experience in `SettingsView.vue` with real-time tier and expiry display.
