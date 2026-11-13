# 76-SEC-MOUNT-DESIGN: Bubblewrap session mount policy (SEC-77-08)

Status: DESIGN ONLY. No source, test, fixture or planning-state file is changed by this document. Nothing here was built or run; every claim about kernel or bwrap behaviour is marked [INFERENCE] until a live control proves it.

Scope: `src/Autonogrammer.Harness.Isolation` (Linux bubblewrap path) and its tests. Seatbelt (macOS) and Windows are out of scope.

## 0. Sources and an unresolved status conflict

- `.planning/STATE.md:53`: SEC-77-08 is open at High. A protected live positive-control run "exposed malformed session bind arguments and failed"; the cause is not recorded in the repo.
- `docs/quality/v0.16.0-security-review.md:43` (SEC-77-08 row) marks the finding `fixed`, citing 31/31 tests with `AUTONOGRAMMER_LIVE_ISOLATION=1` on bubblewrap 0.9.0.
- The two records disagree. This design follows STATE.md (open), as the assignment requires. Owner question OQ9 asks which evidence governs.
- The "fixed" row's live evidence is weaker than it reads: `IsolationLiveTests.ProbePassesAndWriteOutsideGrantIsRefused` asserts `outcome.ReachedFilesystemIsolation` on the probe alone (`tests/Autonogrammer.Harness.Isolation.Tests/IsolationLiveTests.cs:33-34`), i.e. it certifies the probe, not the session policy.
- The root cause of the malformed-bind failure is not identified here. Section 1.4 lists candidates only.

## 1. Current defect (a): probe success is promoted before the session policy runs

### 1.1 What is promoted, and where

- `IsolationProbe.cs:10`: the self-test argv is `--die-with-parent --unshare-user --unshare-pid --ro-bind / / /bin/true`. It contains no grant, no state or scratch bind, no deny mask, no `--dev`/`--proc`, no `--unshare-ipc`/`--unshare-uts`, no `--new-session`.
- `IsolationProbe.cs:69`: a passing self-test returns `IsolationProbeOutcome.Succeeded`.
- `IsolationProbeOutcome.cs:31`: `Succeeded` is `new(IsolationLevel.FilesystemIsolated, null)`. The level is named before any session policy exists.

### 1.2 What already gates it (so the defect is narrower than STATE.md suggests)

- `IsolationPlanner.cs:39` calls `probe.ForVerifiedPolicy(PolicyVerifies(policy))`. `IsolationProbeOutcome.cs:43-53` returns `FilesystemIsolated` only when `BubblewrapArgvBuilder.Verify` accepts the policy, otherwise `None` with `policy_unverified`.
- The planner therefore never reports `FilesystemIsolated` for a policy that fails `Verify`. The residual defects are below.

### 1.3 Residual defects

1. **The only live test certifies the probe.** `IsolationLiveTests.cs:33-34` reads `ReachedFilesystemIsolation` from the probe. No live test launches the session argv and checks what the child can reach.
2. **`Verify` checks a vector that is not launched.** `BubblewrapArgvBuilder.cs:87` verifies `Build(policy)` (canonical). `BubblewrapIsolator.cs:29` launches `AdaptToHost(policy, Build(policy))`. `AdaptToHost` changes the vector: it drops absent deny targets (`:147-151`), turns file denies into `--ro-bind /dev/null` (`:152-157`), drops absent read-only grants (`:175-180`) and resolves symlinked parents for file masks only (`:156`, `ResolveHostPath` at `:110-120`). None of that is verified.
3. **`Verify` is static.** It parses argv and never runs bwrap (`:84-102`, documented at `:77-79` as "does not look at the host"). It cannot show that the kernel enforces the ordering. Correct ordering on paper is not an enforced mount. [INFERENCE: bwrap applies mounts in argv order, as the golden comments assume.]
4. **The namespace sets differ.** The probe uses `--unshare-user` (`IsolationProbe.cs:10`). The session argv (`BubblewrapArgvBuilder.cs:68-72`) does not. [INFERENCE: the session therefore runs in whatever user-namespace mode bwrap picks on the host, which the probe never tested.]

### 1.4 The malformed-bind failure: candidates only [INFERENCE]

The protected run's argv is not in the repo, so these are hypotheses to check against it:

- (i) A directory deny that is reached through a symlinked parent. `AdaptToHost` resolves parents only for file masks (`:152-157`); directory `--tmpfs` entries (`:147-151`) use the unresolved path, and the comment at `:105-106` states that a mount point through a symlink in the read-only root cannot be created.
- (ii) A `--ro-bind /dev/null <target>` file mask onto a path that is not a regular file (for example a socket reached through a link). `File.Exists` is assumed to return true for a socket [INFERENCE].
- (iii) A relative `PI_CODING_AGENT_DIR`. `StateDirectories` (`IsolationPolicy.cs:61`) passes it through and `RequireAbsolute` (`:254-267`) would reject it. That path yields `policy_unverified`, not a launch-time malformed bind, so it is the least likely.

Owner action: supply the exact argv from the protected run (OQ10). The fix in section 3 removes classes (i) and (ii) by construction, but the failing argv decides it.

## 2. Required properties (b)

- **P1 Deny precedence.** A path under a deny target is inaccessible unless an explicit grant names it. Today an explicit grant equal to, or nested under, a deny is re-bound after the mask (`BubblewrapArgvBuilder.cs:56-62`; equality is included by `IsWithin` at `:398`). Tests `NestedWriteGrantUnderADenyIsReopenedOnlyAfterTheMask` and `NestedReadOnlyGrantUnderADenyIsReopenedAfterTheMask` require this. Whether a grant may reopen a deny at all is an owner decision (OQ1).
- **P2 Nested grant boundaries.** A nested grant keeps its own mode. Ancestors must be mounted before descendants regardless of read-write or read-only kind. Today read-write binds (`:42-48`) are emitted before the read-only grants (`:231-237`), so a read-only ancestor can be emitted after a read-write descendant. [INFERENCE: whether a later recursive ancestor bind clears the descendant's writability is unknown and must be shown by control N-nest (section 5).]
- **P3 Socket masking.** No unix socket reachable by filesystem path may remain connectable from the child, except by an approved exception. Masked today: `/run/user` (directory tmpfs), `/run/docker.sock`, `/var/run/docker.sock` (`IsolationDeniedDirectories.SocketLocations`). Not masked: any socket under `/tmp` (the writable `/tmp` overlay was removed, so `/tmp` is now the read-only root), e.g. an `SSH_AUTH_SOCK` under `/tmp/ssh-*`. [INFERENCE: `connect()` on a socket inside a read-only bind still succeeds, so a read-only mount alone does not block it.] Scrubbing the environment names (`BubblewrapArgvBuilder.cs:16-22`) removes the pointers, not the sockets. Abstract-namespace sockets are not filesystem objects and no mount hides them; only `--unshare-net` does, and it is not used (OQ4).
- **P4 Verified equals launched.** The vector that is verified is byte-for-byte the vector that is launched, after host adaptation.
- **P5 Fail closed.** Writes outside grants, scratch and state are refused (existing; root stays read-only, `BubblewrapArgvBuilder.cs:6-7`). An inexpressible or inconsistent grant refuses the session before launch. A failed session canary (section 4) never yields `FilesystemIsolated`.

## 3. Proposed mount layout (c)

### 3.1 Canonical order (host-independent `Build`)

Each line is an ordered bwrap argument group. "Reason" is the ordering rationale.

1. `--ro-bind / /`. Reason: base layer; everything below overlays it. (Unchanged, `:39`.)
2. `--dev /dev --proc /proc`. Moved up from `:64-67`. Reason: these mount fresh trees over `/dev` and `/proc`; placing them before the grants means no grant is shadowed by them. Grants under `/dev`, `/proc` or `/sys` are refused (see 3.3).
3. All grant and state binds, sorted by destination depth (ancestors first), ties kept in input order. The group includes read-write grants, scratch, state directories and read-only grants, each as `--bind p p` or `--ro-bind p p`. Binds whose destination lies under a deny target are excluded here and move to step 5. Reason: P2. A read-only ancestor is always mounted before a writable descendant, and a read-only child is always mounted after its writable parent. Replaces the two passes at `:42-48` and `:56-62` for non-denied targets.
4. `--tmpfs <deny>` for each home deny and socket location (`:50-54`). For a file target, `AdaptToHost` substitutes `--ro-bind /dev/null <target>` (`:152-157`). Reason: a mask must be a directory tmpfs or a non-directory bind; it must come after step 3 so it hides inherited binds, and before step 5 so an explicit reopen is ordered after it.
5. Explicit-grant binds whose destination lies under a deny, each after its mask. Reason: P1. Present only if OQ1 approves reopening. State and scratch never appear here (OQ2).
6. `--unshare-pid --unshare-ipc --unshare-uts --die-with-parent --new-session` (`:68-72`). `--unshare-user` is added only if OQ5 says so.
7. `--chdir <dir>` is appended by `BubblewrapIsolator.cs:31-33`, then the command. (Unchanged.)

### 3.2 Host adaptation and verification

- `Verify` takes the adapted vector, not the canonical one: `Verify(policy, AdaptToHost(policy, Build(policy)))`. The planner (`IsolationPlanner.cs:51-` `PolicyVerifies`) and the wrapper (`BubblewrapIsolator.cs:29`) must use the same value.
- `Verify` gains: (a) for every pair of binds where one is an ancestor of the other, the ancestor precedes the descendant; (b) every mask is a `--tmpfs` directory or a `--ro-bind /dev/null` file, and each is preceded by no bind of a path under it except step 5 binds; (c) no state or scratch path lies under a deny.
- `RequireDenyPrecedence` (`:360-379`) currently ignores `--ro-bind` masks whose source is `/dev/null`. It must include them.

### 3.3 Code changes, with exact lines

| File | Lines | Change |
|---|---|---|
| `BubblewrapArgvBuilder.cs` | 32-74 | Reorder `Build` to steps 1-7; sort step 3 by depth; remove the separate reopen pass for non-explicit binds; move `--dev/--proc` (`:64-67`) to step 2. |
| `BubblewrapArgvBuilder.cs` | 84-102 | `Verify` takes the adapted vector (or verifies `AdaptToHost(Build)`); add ancestor-order and mask-shape checks (3.2). |
| `BubblewrapArgvBuilder.cs` | 147-151 | Resolve symlinked parents for directory masks too (`ResolveHostPath`). |
| `BubblewrapArgvBuilder.cs` | 210-240 | Refuse state or scratch under a deny (OQ2); refuse grants under `/dev`, `/proc`, `/sys`. |
| `BubblewrapArgvBuilder.cs` | 242-252 | Deny list unchanged unless OQ3 adds `/tmp` socket handling. |
| `BubblewrapArgvBuilder.cs` | 254-267 | Refuse a grant equal to or under a deny unless OQ1 approves reopening; refuse `/`, `/home` or ancestors of HOME if OQ8 says so. |
| `BubblewrapArgvBuilder.cs` | 360-379 | Include `/dev/null` file masks in precedence checks. |
| `IsolationProbe.cs` | 10 | Align namespace flags with the session (OQ5), or replace the self-test with the session canary (section 4). |
| `IsolationProbe.cs` | 69 | Return a non-promoting success (level `None`), not `Succeeded`. |
| `IsolationProbeOutcome.cs` | 31, 35, 43-53 | `Succeeded` no longer names `FilesystemIsolated`; promotion happens only after the session canary passes. |
| `IsolationPlanner.cs` | 39 | Promote only on session-canary success; new degraded code (OQ11). |
| `BubblewrapIsolator.cs` | 29 | Unchanged call, but the same adapted vector is verified (3.2). |
| `IsolationLiveTests.cs` | 20, 33-36 | Use a synthetic fixture home (OQ6); assert the session canary, not `ReachedFilesystemIsolation`. |
| `BubblewrapArgvBuilderTests.cs` | 195-244, 267, 281 | Nested-grant tests follow the owner's OQ1 answer; add the N-series tests (section 5.2). |
| `Fixtures/golden-bwrap-argv.txt` | all | Regenerated for the new order (OQ10). |

No change to `Scrub` (`:198-208`) is proposed, beyond noting that it removes names, not sockets (P3).

## 4. Session canary (new live step, runs at session start)

Run the exact launched argv (`BubblewrapIsolator.Wrap` output, adapted) with `/bin/sh -c '<canary>'`. Promote to `FilesystemIsolated` only if the canary exits 0. Any failure yields level `None`, the new degraded code, and `NeedsConsent`. Writes go only to the session scratch directory.

Canary checks (each `|| exit N` names the failure):

- C1 read-only root: `echo x > /etc/autonogrammer-canary 2>/dev/null && exit 21`
- C2 deny masked (each existing home deny `$D`): `[ -z "$(ls -A "$D" 2>/dev/null)" ] || exit 22`
- C3 socket masked: `[ ! -S "/run/user/$(id -u)/bus" ] || exit 23`; `[ -z "${SSH_AUTH_SOCK-}" ] || exit 24`
- C4 scratch writable: `echo ok > "$SCRATCH/.canary" || exit 25`; then `rm -f "$SCRATCH/.canary"`
- C5 pid namespace: `[ "$(ls /proc | grep -cE '^[0-9]+$')" -le 8 ] || exit 26` [INFERENCE: a small process count inside a fresh pid namespace; the threshold needs a measured host value]

Canary C2 reads nothing from the masked directories, so it does not expose secrets to the test output.

## 5. Controls

Expected results are stated before any run. "Today" means the current code, per the reading in section 1; those are predictions to confirm, not observations.

### 5.1 Positive controls (must pass after the change)

Prerequisites: `bwrap --version` succeeds; user namespaces permitted (distribution-dependent [INFERENCE]); owner runs the commands. Fixture paths are created under `F`, not the real home.

Fixture (owner runs; the shell expands paths before bwrap starts, so the canary strings below contain literal paths):

```
F=$(mktemp -d); H=$F/home; G=$F/grant; S=$F/scratch
mkdir -p "$H/.ssh" "$H/.omp" "$G/ro/out" "$S"
echo key > "$H/.ssh/key"; echo ro > "$G/ro/file"
```

Hand-run of the step-3 ordering (depth-sorted, ancestors first), with the deny mask at step 4:

```
bwrap --ro-bind / / --dev /dev --proc /proc \
  --bind "$G" "$G" --bind "$S" "$S" --bind "$H/.omp" "$H/.omp" \
  --ro-bind "$G/ro" "$G/ro" --bind "$G/ro/out" "$G/ro/out" \
  --tmpfs "$H/.ssh" \
  --unshare-pid --unshare-ipc --unshare-uts --die-with-parent --new-session \
  --chdir "$G" /bin/sh -c '
    echo ok > "$G/ro/out/new"   || exit 31
    echo x  > "$G/ro/file" 2>/dev/null && exit 32
    echo ok > "$G/newfile"      || exit 33
    [ -z "$(ls -A "$H/.ssh" 2>/dev/null)" ] || exit 34
    [ ! -S "/run/user/$(id -u)/bus" ]       || exit 35
    echo x  > /etc/autonogrammer-canary 2>/dev/null && exit 36
    exit 0'; echo "exit=$?"
```

Pass: `exit=0`. Each `exit N` names the control that failed. Expected today: exit 0 for the grant and ro checks; the ordering results depend on bwrap behaviour (P2 [INFERENCE]).

Then the repository controls (owner runs after the change):

```
AUTONOGRAMMER_LIVE_ISOLATION=1 dotnet test tests/Autonogrammer.Harness.Isolation.Tests -c Release --no-build --filter-trait "Category=Live"
```

Positive controls the live tests must cover:

- PC1 rw grant writable and the file persists on the host (`ProbePassesAndWriteOutsideGrantIsRefused` already covers part; extend it).
- PC2 ro grant readable, not writable (`echo x > "$G/ro/file"` must fail).
- PC3 rw grant nested under an ro grant is writable; sibling under the ro grant is not (P2).
- PC4 scratch writable; state (`~/.omp`) writable.
- PC5 session canary (section 4) passes on a host with bwrap; the planner then reports `FilesystemIsolated`.

### 5.2 Negative controls (must fail closed)

- N-a rw grant equal to `$H/.ssh`: must throw `MalformedArgvException` before argv is built. Today: accepted and re-bound (`BubblewrapArgvBuilder.cs:58-60`, `IsWithin` `:398`). Expected to fail today.
- N-b `PI_CODING_AGENT_DIR=$H/.ssh/omp`: must be refused (OQ2). Today: `RequireDenyPrecedence` (`:372-376`) only flags a mask ordered before a bind, so the state bind after the mask passes. Expected to fail today.
- N-c relative grant `grant/rel`: must throw (existing: `IncompleteWritableGrantIsRejectedWithATypedError`). Passes today.
- N-d rw source missing on host: must throw `InvalidOperationException` (existing: `AdaptToHostThrowsWhenAWriteBindSourceIsMissing`). Passes today.
- N-e launched-vector mismatch: a policy where `AdaptToHost` drops a required bind must fail `Verify` on the adapted vector. Today `Verify` checks the canonical vector and passes. Expected to fail today; this proves defect 1.3(2).
- N-f canary failure: a test double that omits the scratch bind must produce level `None`, the new degraded code, `NeedsConsent=true`. New test.
- N-g unverified policy: `UnverifiedPolicyNeverReportsFilesystemIsolation` (existing). Passes today.
- N-h docker socket: with `/run/docker.sock` present on the host, `test -S /run/docker.sock` inside the sandbox must be false. Skipped where no socket exists.
- N-nest: rw grant `$G/ro/out` under ro `$G/ro` written after an ancestor-first mount must still be writable and `$G/ro/file` must not. Confirms P2; fails if bwrap applies later recursive binds over the child's mode [INFERENCE].
- N-tmp: a socket at `/tmp/ssh-x/agent.1` reachable from the child (`[ -S ... ] && exit`). Expected to fail today (P3 gap) until OQ3 is answered.

## 6. Rollout order (for the owner's approval)

1. Answer OQ1 to OQ11.
2. Obtain the protected run's argv (OQ10) and confirm which of 1.4 (i)-(iii) it is.
3. Implement section 3 and section 4 in one change; regenerate the golden fixture.
4. Run the section 5 controls on a host with bwrap; record each result against its expected value.
5. Only then change SEC-77-08 status. The owner's approval is required for that status change.

## 7. Open questions for the owner (f)

- OQ1 Deny versus explicit grant. Should a grant equal to or under a deny be refused (this design's recommendation, matching "deny precedence over grants" in P1 [INFERENCE that this is the owner's intent]) or should it reopen as the current tests require?
- OQ2 State and scratch under a deny (for example `PI_CODING_AGENT_DIR` under `~/.ssh` or `~/.local/share/Autonogrammer`, which is itself denied at `IsolationDeniedDirectories.ForHome`). Refuse, or treat as a grant?
- OQ3 `/tmp`. Keep the read-only root and mask socket files found under `/tmp` by enumeration (feasibility in .NET not verified), or restore a writable `/tmp` tmpfs? The second contradicts the stated refusal of writes outside grants (`BubblewrapArgvBuilder.cs:6-7`).
- OQ4 Abstract sockets and network. Accept them (the chip says `network: unrestricted`), or add `--unshare-net` with a proxy design? Until answered, P3 is incomplete for abstract sockets.
- OQ5 Probe namespaces. Align the probe with the session (drop `--unshare-user`), or keep it and rely on the canary?
- OQ6 Live test home. Use a synthetic fixture home (recommended), not `Environment.SpecialFolder.UserProfile` (`IsolationLiveTests.cs:20`).
- OQ7 Supported bwrap versions. The security review cites only 0.9.0.
- OQ8 Root grants. Refuse grants of `/`, `/home`, or ancestors of HOME?
- OQ9 Status conflict. `docs/quality/v0.16.0-security-review.md:43` says fixed; `STATE.md:53` says open after a failing protected run. Which record governs, and who signs the status change?
- OQ10 The exact argv of the protected run that failed, and approval to regenerate `golden-bwrap-argv.txt`.
- OQ11 Approve the new degraded code name (proposed: `session_policy_unenforced`) and its consent behaviour.

## 8. Not verified here

- Nothing in this document was built, run or tested. All bwrap semantics beyond argv order, the identity of the malformed-bind cause, `File.Exists` on sockets, and the process-count threshold in C5 are [INFERENCE].
- `docs/quality/v0.16.0-security-review.md` shows as modified in the working tree; this design did not change it.
