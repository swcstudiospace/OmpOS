# Phase 76 owner decisions (2026-11-13)

Binding answers to the open questions in `76-SEC-MOUNT-DESIGN.md`, given through the planning form. Recorded here so they outlive the conversation.

| Question | Decision | Consequence recorded |
| --- | --- | --- |
| OQ1 grant equal to or under a deny | Reopen: an explicit grant may reopen a denied path. Weakens deny precedence (design property P1). | `EqualWriteGrantUnderADeny` tests the reopen. |
| OQ2 state or scratch under a deny | Treat as a grant (for example `PI_CODING_AGENT_DIR` under `~/.ssh`). Weakens deny precedence. | Refuse-state rule not applied. |
| OQ3 /tmp | Fresh writable tmpfs inside the sandbox; host /tmp never bound. Narrows the write-refusal property to exclude /tmp. | `--tmpfs /tmp`; `TmpIsAFreshTmpfsAndTheHostTmpIsNeverBound`. |
| OQ4 network and abstract sockets | Draft only. `--unshare-net` not implemented. Design: `76-OQ4-PROXY-DESIGN.md`, awaiting owner answers Q1 to Q8 and lifting of D-20 / OQ-452. | Abstract sockets and network remain an open gap. |
| OQ5 probe namespaces | Keep the probe; rely on the session canary. | Promotion only after the canary passes. |
| OQ6 live test home | Synthetic fixture home. | `IsolationLiveTests` uses a fixture home. |
| OQ7 bubblewrap versions | 0.9.0 only. | - |
| OQ8 root grants | Refuse grants of /, /home, or ancestors of HOME. | `RequireBindable`. |
| OQ9 status conflict | STATE governs. SEC-77-08 is open. The live probe test asserts only the probe. | Security review row corrected to open (commit 6651919f). |
| OQ10 protected-run argv | Argv not supplied. Bind order fixed by construction; the original malformed-bind root cause stays unidentified. | Canary run before promotion. |
| OQ11 degraded code | Approved: `session_policy_unenforced`, with the consent behaviour in the design. | `SessionCanaryFailed`. |

Not changed by these decisions: SEC-77-08 stays open until its own verification and an owner status change. The architecture release gate continues to fail for it.
