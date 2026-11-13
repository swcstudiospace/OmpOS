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

## OQ4 network proxy (D-20 lifted)

Source: `76-OQ4-PROXY-DESIGN.md` section 9. Owner instruction, 2026-11-13: D-20 and OQ-452 are lifted for the Phase 76 network proxy, and Q1 to Q8 are answered with the design's recommended defaults. Where the design gives no default, the row says so and no default is assumed.

| Question | Recommended default (from design) | Status |
| --- | --- | --- |
| Q1. Lift D-20 and OQ-452 for this scope? | No default in the design. The design states it cannot be built without the lift (sections 0 and 9 Q1). | Owner answer: lifted for the Phase 76 network proxy. This is the owner's own decision, not a design default. |
| Q2. Which option? | Option B: host-side proxy reached over a bound unix socket, staged behind a default-off setting, chip unchanged until live. Option D is the later end state if transparent egress is required. Option A is not viable as worded. | answered by owner default, 2026-11-13 |
| Q3. Allow-list source and default? | Provider endpoints from configuration only, each an exact host on port 443. Remote plugin and MCP hosts from configuration. Git hosts and registries default off. The design labels this list "owner to confirm" (section 5). | answered by owner default, 2026-11-13 |
| Q4. Same-user host processes can use the 0600 socket: accept, or require a token in the first protocol line? | No default given. | no default given; owner answer required |
| Q5. Accept DNS rebinding and TCP-only coverage as stated limits, or require UDP filtering (option D)? | No default given. Context only: section 5 lists the rebinding check as a deny requirement, and section 4 shows UDP blocked under option B. | no default given; owner answer required |
| Q6. Refuse session start if the proxy cannot start, or degrade to C1 with consent? | No recommended default. Section 9 Q6 says the SEC design OQ11 pattern (`session_policy_unenforced`) "would apply"; this is a pointer, not a stated default. | no default given; owner answer required |
| Q7. Bridge binary: ship a small helper, or use a binary the host already has? | No default given. | no default given; owner answer required |
| Q8. Accept the option D dependency (pasta or slirp4netns) and its version pinning? | No default given. Under the Q2 default (option B) option D is not needed (section 3). | no default given; owner answer required |

Q1 to Q3 are recorded. Q4 to Q8 remain open. Plan steps that depend on them are blocked in `76-OQ4-IMPLEMENTATION-PLAN.md`.
