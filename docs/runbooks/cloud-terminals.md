# Cloud terminals on Railway runbook

Scope: Phase 42 (VX-06, VX-08). Each cloud session runs in a Linux environment that sleeps when idle and restores its transcript on reattach. Status as of 2026-10-26.

This record states status only. Nothing in it is built, run or deployed by this document. Phase 42 is a prototype only: ROADMAP allows no production deploy until criterion 2 is met.

## Sources

- `.planning/ROADMAP.md`, Phase 42 success criteria 1 to 3.
- `.planning/STATE.md`, Plan Decisions and Assumptions (2026-10-22): the owner decision on the cloud model, and the open pauses.
- `docs/plans/vision-research/railway-cloud-desktop.md`, rows 54 and 55 (Trust and Safety), and owner decisions 1 and 2. Read on 2026-10-22.
- `docs/runbooks/hosting.md`, Known blockers 1 and 5.

## Status key

- **MET**: the criterion holds and the repository shows it.
- **NOT STARTED**: no code or contract exists for it.
- **BLOCKED**: a named external prerequisite is missing. The owner and route are in the entry.

## Criteria

| # | Criterion (ROADMAP, summarised) | Status | Evidence or blocker |
| --- | --- | --- | --- |
| 1 | One persistent container per session with idle sleep, one container plus volume for the transcript, reattach restores the transcript, integration test against a fake Railway client, live run recorded blocked | PARTIAL (library over a fake, wired nowhere); live run BLOCKED | `CloudSessionHost`, `ICloudEnvironmentClient` and `FakeCloudEnvironmentClient` exist in `src/OmpOS.Sandbox`, tested by `CloudSessionHostTests`; no head or API uses them (corrected 2026-10-27; this row said none existed). `src/OmpOS.Sandbox/RailwaySwarmProvider.cs` exists and fails closed for provision, exec, logs and attach; only service deletion is callable (Q-SB-RAILWAY). |
| 2 | Remote desktop and browser streaming is not built until Railway confirms in writing; the confirmation is recorded under `docs/runbooks/` | PAUSED: BLOCKED on Railway | No confirmation exists. The pause is recorded below. |
| 3 | No claim that process state survives sleep; a restored session resumes from the stored native omp session reference; a test shows the stored transcript and a resume marker | PARTIAL (library, fake only) | The reference type `Harness.Herdr/NativeSessionReference.cs` and `HerdrSessionService.ResumeFromNativeReferenceAsync` exist (tested by `ALostSessionIsNotRunningUntilItResumesFromItsNativeReference`). No transcript volume and no resume marker exist. |

Note (2026-10-27): the criteria 1 and 3 rows above were written before `CloudSessionHost` landed. `CloudSessionHostTests` (`EachSessionGetsOneContainerAndOneVolume`, `IdleSessionSleepsAfterTheConfiguredWindow`, `ReattachRestoresTheTranscriptFromTheVolume`, `NoProcessStateAcrossSleepIsClaimed`, `RestoredSessionShowsTheStoredTranscriptAndAResumeMarker`) cover them over the fake; the audit `docs/quality/cursor-parity-audit-2026-10-26.md` records the position.

## Criterion 2: pause

Remote desktop and browser streaming for cloud sessions is **not built and not started**. The pause holds until Railway confirms in writing that an agent-driven browser desktop is permitted. Nothing in this phase builds toward it: no VNC, noVNC or browser-streaming code, no public domain for a desktop view and no TCP proxy for one.

The research found no primary statement that settles the question. Whether Railway would treat an agent-driven browser desktop as a prohibited remote desktop cannot be determined from documents and needs Railway's answer (`railway-cloud-desktop.md`, Unverified).

## Live run: BLOCKED

The live cloud-terminal run is not performed. Prerequisites that are not met:

1. Railway deploys are paused. `docs/runbooks/hosting.md` (Known blocker 5) states that nothing has been deployed to Railway and that no Railway service or deploy exists while deploys are paused.
2. The sign-in authority is not configured (`docs/runbooks/hosting.md`, Known blocker 1; the OD-03 decision).
3. The cloud runtime needs v0.9.2 Phase 13, which is blocked on the same external prerequisites (ROADMAP).

When the prerequisites are met, the run record must name: the Railway project and environment, the commands run, the sleep and reattach observations, and the transcript shown after reattach. No such record exists.

## Policy text

Railway's Trust and Safety page (https://docs.railway.com/platform/trust-and-safety.md, read 2026-10-22) prohibits "operating proxies, VPNs, anonymization services, or remote desktops for personal or professional browsing". Reverse proxies and private networking tools that serve the user's own services are allowed. Restrictions can affect a service, a feature or a workspace, and one appeal is available. The page invites users to ask through Central Station or abuse@railway.com before deploying when unsure.

Risk recorded by the research (S1): a desktop with a browser used by an agent may be treated as a prohibited remote desktop, and a restriction can take the workspace offline. The wording is verified; its application to this use is unverified.

Recommended route in the research (owner decision 2, option A): ask Railway before any build. Who sends the question and when is an owner decision.

## Owner decisions in force

- Cloud model (answered 2026-10-22): a persistent container per session with idle sleep, restoring the transcript from a volume on reattach.
- Idle stop default: 24 hours (recorded as a numeric assumption in `.planning/STATE.md`, Plan Decisions and Assumptions; not confirmed by the owner).
- Durability across a herdr server restart or machine reboot is a separate owner decision (OQ-76). Default (a): resume from the stored native omp session reference.

## STATE entry for the confirmation

Text for the orchestrator to add under the open pauses in `.planning/STATE.md`:

```text
- EXTERNAL (Phase 42, criterion 2): Railway written confirmation that an agent-driven browser desktop is permitted. No confirmation exists as of 2026-10-26; whether the question was sent to Railway is not recorded. Until the confirmation exists no remote desktop or browser streaming is built, and no production deploy happens. The confirmation is recorded in docs/runbooks/cloud-terminals.md. Tracked in OQ-73.
```

## Open items

1. Railway written confirmation: owner, Railway. Route: OQ-73.
2. Railway deploys and the sign-in authority: owner. Route: OQ-61, OQ-70 and `docs/runbooks/hosting.md`.
3. Fake-backed cloud session, transcript volume and resume marker (criteria 1 and 3): engineering, no external prerequisite.
