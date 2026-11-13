# 76-OQ4-IMPLEMENTATION-PLAN: network proxy for the bubblewrap session

Status: awaiting owner review; no code written.

Basis: `76-OQ4-PROXY-DESIGN.md`, option B (the Q2 recommended default). Q1 to Q3 are recorded in `76-OWNER-DECISIONS.md`, section "OQ4 network proxy (D-20 lifted)". Q4 to Q8 have no answer; steps that depend on them are marked BLOCKED.

Claims the repo does not prove are marked [INFERENCE]. Nothing here was built, run or tested.

## 1. Target behaviour

- Default off. With the setting off, the session argv and child environment are unchanged from today.
- Setting on: the session argv adds `--unshare-net`. A host-side egress proxy runs per session. A sandbox-side bridge forwards `127.0.0.1:<port>` to the proxy's unix socket. The child receives proxy variables that point at the bridge.
- Policy (design section 5): allow-list from configuration; always deny loopback, link-local, private ranges and non-allow-listed IP literals; resolved-address check against rebinding; plain HTTP denied without an allow entry; no TLS termination.
- The chip and settings copy keep `network: unrestricted` until the proxy is live and verified (design section 7). Then all copies change together (S8).

## 2. Ordered steps

S1. Owner answers Q4 to Q8.
- Blocks: S3 (Q4 token, Q6 start failure), S4 (Q7), S5 (Q8 only if option D is chosen), S7 (Q6), S8 (Q5 wording of stated limits).
- Steps S2, S5 (argv part only) and S6 need no answer and may proceed once the owner approves this plan.

S2. Setting and policy surface, default false.
- Add an `init` property beside `Enabled` in `src/Autonogrammer.Harness.Isolation/IsolationPolicy.cs` (line 10 is the existing `Enabled`).
- Add the owner-facing toggle in `src/Autonogrammer.Presentation/Settings/Cowork/IsolationSettingsPage.cs`. Where the value is persisted is not traced here [INFERENCE: follow the existing isolation setting].
- Default-off test: `tests/Autonogrammer.Sandbox.Tests/IsolationDefaultsTests.cs`.

S3. Host egress proxy. BLOCKED on Q4 and Q6.
- New host-side classes in `src/Autonogrammer.Harness.Isolation/` (names proposed, not decided: `EgressProxy.cs`, `EgressAllowList.cs`, `EgressDenyRanges.cs`).
- Listen on a unix socket in a per-session directory under scratch (not `/tmp`; design section 6). Directory mode 0700, socket mode 0600. Unlink a stale socket before bind.
- Accept HTTP `CONNECT` and plain HTTP forward requests. Resolve the host, check the resolved address against deny ranges, connect to that same address, copy bytes.
- Peer check: `SO_PEERCRED` uid must equal the session uid. Same-uid token: per Q4.
- Connection cap and idle timeout: values set by owner [INFERENCE: design leaves values open].
- Log host, port and decision only. No bodies or headers.
- Lifecycle: start before the sandbox launches, stop when the session ends.

S4. Sandbox bridge. BLOCKED on Q7.
- A small helper that listens on `127.0.0.1:<port>` inside the namespace and forwards bytes to the bound unix socket (design section 3 B, step 4).
- Must not depend on `socat` (design section 3 B).
- Shipped artifact or host binary per Q7. If shipped, it is versioned and pinned like other supported binaries [INFERENCE: pinning mechanism not located in repo].

S5. Argv and environment wiring in `src/Autonogrammer.Harness.Isolation/BubblewrapArgvBuilder.cs`.
- a. When on, add `--unshare-net` after `--unshare-uts` (lines 68 to 70). Off: no change.
- b. Bind the proxy session directory at the same absolute path, as a directory, in the grant group after the scratch bind (lines 287 to 289; design section 7). Uses `RequireBindable` like the scratch bind.
- c. Set `HTTP_PROXY`, `HTTPS_PROXY`, `http_proxy`, `https_proxy` for the child to the bridge address. Host values come through `src/Autonogrammer.Harness.Acp/EngineEnvironmentPolicy.cs` lines 33 to 38; with the setting on, the host proxy values must not reach the child. Edit the policy in that file, not the argv builder.
- d. OPEN, not in Q1 to Q8: a user's own upstream proxy (corporate proxy) is not chained by the design. Owner decision needed before S5c is complete.
- e. Confirm `NO_PROXY` handling for the bridge address [INFERENCE].

S6. Session canary network checks. Not blocked.
- Extend `RunSessionCanaryAsync` in `src/Autonogrammer.Harness.Isolation/IsolationProbe.cs` (line 125) with two checks: the child cannot reach a host loopback port; the child reaches an allow-listed host through the proxy.
- The canary argv must be the argv that is launched (design section 7).
- Do not change `SelfTestArguments` (`IsolationProbe.cs` line 14). OQ5 keeps the probe as it is. The design cites line 10; in this tree the array is at line 14.

S7. Failure behaviour. BLOCKED on Q6.
- If the proxy or bridge cannot start, the session either refuses to start or degrades with consent, per Q6.
- Degraded path reuses the existing code `IsolationProbeOutcome.SessionPolicyUnenforced` and `SessionCanaryFailed` (`src/Autonogrammer.Harness.Isolation/IsolationProbeOutcome.cs` lines 33 and 45). Consent flow: `IsolationPlanner.cs`.

S8. UI and copy. BLOCKED on S3 to S6 being live, and on Q5 for the wording of stated limits.
- Current `network: unrestricted` sites, verified in this tree:
  - `src/Autonogrammer.Presentation/Isolation/IsolationChipModel.cs` lines 14 (constant), 34 (field), 48 (level text); doc at line 7.
  - `src/Autonogrammer.Presentation/Settings/SettingsCopy.cs` line 69.
  - `src/Autonogrammer.Presentation/Settings/Permissions/PermissionStatementModel.cs` lines 10 and 22.
  - Doc comments only: `src/Autonogrammer.Presentation/Settings/Cowork/IsolationSettingsPage.cs` line 5; `src/Autonogrammer.Presentation/Settings/SettingsSection.cs` line 87.
- The design names three copies (section 1). This tree has more; all must change together.
- New wording: owner approval required.

S9. Documentation. Only after the owner approves the status change.
- `76-SEC-MOUNT-DESIGN.md` P3 (abstract sockets) status.
- `76-CONTEXT.md` D-20 (line 47) and OQ-452 (lines 108 and 109) wording.

S10. Tests, listed in section 4.
- Unit: `tests/Autonogrammer.Harness.Isolation.Tests/BubblewrapArgvBuilderTests.cs` (existing `--unshare-pid` assertion at line 21 is the pattern).
- New: an egress proxy test class in `tests/Autonogrammer.Harness.Isolation.Tests/` (name proposed).
- Environment: `tests/Autonogrammer.Harness.Acp.Tests/AcpEnvironmentIsolationTests.cs`.
- Live: `tests/Autonogrammer.Harness.Isolation.Tests/IsolationLiveTests.cs`, synthetic fixture home (OQ6).
- Presentation: `tests/Autonogrammer.Presentation.Tests/PresentationIsolationTests.cs`; `tests/Autonogrammer.Native.Tests/Settings/IsolationPageViewTests.cs`.
- Architecture: `tests/Autonogrammer.ArchitectureTests/` only if a new project is added [INFERENCE].

S11. Rollout. Ship default off. Any change of default is a separate owner decision.

## 3. Files touched (exact paths)

| Path | Change | Step | Blocked by |
| --- | --- | --- | --- |
| `src/Autonogrammer.Harness.Isolation/IsolationPolicy.cs` | Add default-false setting | S2 | none |
| `src/Autonogrammer.Presentation/Settings/Cowork/IsolationSettingsPage.cs` | Toggle; doc line 5 | S2, S8 | none / Q5 |
| `src/Autonogrammer.Harness.Isolation/EgressProxy.cs` (new, proposed) | Host proxy | S3 | Q4, Q6 |
| `src/Autonogrammer.Harness.Isolation/EgressAllowList.cs` (new, proposed) | Allow-list from config (Q3 answered) | S3 | none |
| `src/Autonogrammer.Harness.Isolation/EgressDenyRanges.cs` (new, proposed) | Deny ranges | S3 | none |
| Bridge project or binary (new; location per Q7) | Sandbox forwarder | S4 | Q7 |
| `src/Autonogrammer.Harness.Isolation/BubblewrapArgvBuilder.cs` | `--unshare-net`, dir bind, env | S5 | S3, S4 |
| `src/Autonogrammer.Harness.Acp/EngineEnvironmentPolicy.cs` | Override proxy vars when on | S5c | Item S5d (owner decision) |
| `src/Autonogrammer.Harness.Isolation/IsolationProbe.cs` | Canary network checks | S6 | S5 |
| `src/Autonogrammer.Harness.Isolation/IsolationProbeOutcome.cs` | Reuse existing codes only | S7 | Q6 |
| `src/Autonogrammer.Harness.Isolation/IsolationPlanner.cs` | Consent path, if Q6 requires | S7 | Q6 |
| `src/Autonogrammer.Presentation/Isolation/IsolationChipModel.cs` | Copy | S8 | S3 to S6, Q5 |
| `src/Autonogrammer.Presentation/Settings/SettingsCopy.cs` | Copy | S8 | S3 to S6, Q5 |
| `src/Autonogrammer.Presentation/Settings/Permissions/PermissionStatementModel.cs` | Copy | S8 | S3 to S6, Q5 |
| `src/Autonogrammer.Presentation/Settings/SettingsSection.cs` | Doc line 87 | S8 | S3 to S6 |
| `76-SEC-MOUNT-DESIGN.md`, `76-CONTEXT.md` | Status | S9 | owner approval |

## 4. Controls

Each control states its expected result before it runs. None has been run.

Positive:
- P-argv: setting on gives argv with `--unshare-net`, the proxy directory bind after the scratch bind, and bridge proxy variables. Expected: all present.
- P-off: setting off gives argv equal to today's argv. Expected: no `--unshare-net`; chip still reads `network: unrestricted`.
- P-allow: `CONNECT` to an allow-listed host on 443 succeeds and logs `allowed`.
- P-net1 (design section 8): a model API request completes through the bridge with the real provider configuration in a synthetic fixture home. Expected: success from an allow-listed host.
- P-canary: the session canary passes with the setting on.

Negative (must fail closed):
- N-net1: sandbox connection to a host loopback port fails.
- N-net2: sandbox connection to host abstract socket `@test-abstract` fails. Run it first against the current build to confirm the gap exists; expected today: it connects [INFERENCE].
- N-net3: `CONNECT` to a denied host fails and logs a denial.
- N-net4: a hostname resolving into a denied range fails (rebinding).
- N-net5: a socket file owned by another host user is refused by the peer check. Expected: refused. Needs a second account [INFERENCE: may be impractical]. Depends on Q4.
- N-net6: an IP literal not on the allow-list fails.
- N-net7: plain HTTP without an allow entry fails.
- N-env: with a host `HTTPS_PROXY=http://127.0.0.1:3128`, the child's proxy is the bridge address, not the host value.
- N-stale: a stale socket from a crashed run is unlinked before bind; the session starts.
- N-start-fail: proxy fails to start. Expected: behaviour matches the Q6 answer.

## 5. Verification commands (proposed; not run)

- `bwrap --version`. Expected: `0.9.0` (OQ7) [INFERENCE: output format not checked].
- `dotnet test tests/Autonogrammer.Harness.Isolation.Tests --filter "FullyQualifiedName~BubblewrapArgvBuilder"`
- `dotnet test tests/Autonogrammer.Harness.Isolation.Tests --filter "FullyQualifiedName~IsolationLive"` [INFERENCE: live tests need bwrap; gating not checked]
- `dotnet test tests/Autonogrammer.Harness.Acp.Tests --filter "FullyQualifiedName~AcpEnvironmentIsolation"`
- `dotnet test tests/Autonogrammer.Presentation.Tests --filter "FullyQualifiedName~PresentationIsolation"`
- `dotnet test tests/Autonogrammer.Sandbox.Tests --filter "FullyQualifiedName~IsolationDefaults"`
- `dotnet test tests/Autonogrammer.ArchitectureTests` only if a project is added [INFERENCE].
- Static check: `grep -rn "unshare-net" src tests`. Expected after S5: hits only in `BubblewrapArgvBuilder.cs` and its tests.

## 6. Unverified points to resolve before the relevant step

- Whether loopback is up inside the new namespace [INFERENCE]. Blocks S4.
- Whether omp honours `HTTPS_PROXY` for every provider it calls [INFERENCE]. Blocks S5 end-to-end; covered by P-net1.
- Whether abstract sockets are reachable from a real desktop session [INFERENCE]. Covered by N-net2 baseline.
- DNS inside a namespace with no resolver [INFERENCE]. Affects clients that do not send hostnames through `CONNECT`.
- Whether the peer-credential check works across the bind [INFERENCE]. Blocks N-net5.
- Item S5d (upstream proxy chaining): owner decision, not yet asked.
