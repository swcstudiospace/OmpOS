# Phase 1 negative proofs, 2026-10-21

Run in a clean clone of committed HEAD `dc1f90c` (branch `feat/v0.9.2-hardening`), in `/tmp`, so the working tree was never mutated. The mutations were reverted before the clean runs.

| Log | Command | Exit | Meaning |
| --- | --- | --- | --- |
| `proof-format-broken.log` | `dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore` after appending two blank lines to `src/OmpOS.Contracts/AssemblyMarker.cs` | 0 | The stray trailing blank line does not fail the verifier. The row's original mutation is withdrawn. The log is empty because a passing verify prints nothing. |
| `proof-format-indent.log` | Same command after adding `src/OmpOS.Contracts/ProofMarker.cs` with a two-space member | 2 | Fails as required: `error WHITESPACE` at `ProofMarker.cs(6,3)`. This is the negative proof for row 1-01-03. |
| `proof-format-clean2.log` | Same command after the file is deleted | 0 | The clean tree passes. The log is empty for the same reason. |
| `proof-restore-drift.log` | `dotnet restore OmpOS.Linux.slnf --locked-mode` after `Markdig` is changed from 1.4.2 to 1.4.1 in `Directory.Packages.props` | 1 | Fails as required with `NU1004` (the lock file is inconsistent). This is the negative proof for row 1-01-09. |
| `proof-restore-clean.log` | Same command after the file is restored | 0 | The clean tree restores. |

Baseline: `restore --locked-mode` on the clean clone exits 0 (recorded in the proof run, not kept as a log).
