# Phase 77: Hardening, docs and integration - Context
Source: docs/plans/v0.16.0-research/briefs/77-hardening-and-integration.md
**Gathered:** 2026-10-28
**Status:** Ready for planning

<domain>
## Phase Boundary

The last phase of milestone v0.16.0. After phases 67 to 76 have landed and the rename (phase 66) is applied, it brings the milestone to a shippable state: one version bump (`VersionPrefix` 0.16.0, one relock), final documentation, the Cowork parity audit `docs/parity/cowork-parity.md` that cannot contain an empty row, a written security review and accessibility pass over the new surfaces, the full gate, integration with the other line of work, a PR, and the list of actions only the owner can do. Requirements AG-77-1 to AG-77-17; units U1 to U9 (brief 77); owner questions OQ-480 to OQ-485 (`docs/open-questions.md:314-319`, all open with defaults).

Dependencies (index row 77): phases 67 to 76 landed, each with its own gate green and its Critical and High findings closed; 66 applied; the scaffold step (`00-scaffold-manifest.md`: projects registered, `ContractVersion` 1.6.0); `docs/parity/cowork-parity.md` created by 75-06 and amended by 76-06. No ADR of its own: ADR-0037 D14 (parity audit, cross-phase ownership) over all of ADR-0030 to ADR-0037.

Must NOT do: add any feature (source edits only to fix a U4 or U5 finding, each with a regression test, listed in the findings table); any live run (OD-07 not approved, no Supabase, no Railway); Windows or macOS runs; a release tag; any rename outside the repository; perform any owner action; pass the gate by downgrading a High finding or by a default-off flag.

</domain>

<decisions>
## Implementation Decisions

### Order and scope
- **D-01:** Unit order: U7 (integration with the other line) first because the rebrand relocks; then U3 to U5 in parallel (parity gate, security review, accessibility pass); then U2 (final docs), U1 (version bump, last before the gate because a bump relocks everything), U6 (full gate), U8 (PR and ship); U9 (owner-action list) is written with U2 and performed by the owner. (brief 77 Work units)
- **D-02:** Source code is edited only to fix a finding of U4 or U5, each with its regression test, and each edit is listed in the findings table. (brief 77 Projects and files owned)
- **D-03:** A phase's own runbook is completed, not rewritten (`docs/runbooks/{accounts,data-plane,cowork,dispatch,browser-use,computer-use}.md`, `v0.16.0-owner-actions.md`). (brief 77 Projects and files owned)

### Version bump and relock (U1)
- **D-04:** `VersionPrefix` in `Directory.Build.props` becomes `0.16.0` (it is `0.15.0` at `Directory.Build.props:8`) and the container version `ARG` default equals it; the change is ONE commit, made after every other unit, so `git log -S'<VersionPrefix>0.16.0'` finds exactly that commit. On disk only `deploy/api.Containerfile:102` (`ARG AUTONOGRAMMER_VERSION=0.15.0`) carries a version default; `deploy/worker.Containerfile` has none (brief 77 AG-77-1 says both). (AG-77-1; report 09 D2 and s.2 last guard)
- **D-05:** The relock runs ONCE: `dotnet restore Autonogrammer.Linux.slnf --force-evaluate -p:RestoreLockedMode=false` with `obj/` and `bin/` deleted and `CI` unset; the Android project restores on its own; afterwards `--locked-mode` succeeds, no later commit touches a `packages.lock.json`, and `git diff --stat` of the bump commit shows only version files and locks (85 locks at v0.15.0 plus the 40 of the scaffold: count them). `THIRD-PARTY-NOTICES.md` is regenerated only if the relock changes it. (AG-77-2; brief 77 U1)
- **D-06:** `CHANGELOG.md` gets a `## [0.16.0]` section built from the Unreleased lines of the phases plus a compare link; `[Unreleased]` is empty afterwards; `bash scripts/ci/check-changelog.sh` passes. The compare-link repo URL stays as is until the owner renames the repository (`CHANGELOG.md:267` names `swcstudiospace/OmpOS`). (AG-77-3; rebrand runbook s.5)
- **D-07:** `ContractVersion.Current` is `1.6.0` and no commit after the scaffold commit touches `ContractVersion.cs`: the bump is the scaffold's, never this phase's. (AG-77-4; R9)

### Documentation (U2)
- **D-08:** Six runbooks plus `v0.16.0-owner-actions.md` under `docs/runbooks/` (`accounts.md`, `data-plane.md`, `cowork.md`, `dispatch.md`, `browser-use.md`, `computer-use.md`), listed in `docs/SUMMARY.md` and passing `scripts/docs/check_gitbook.py`; every page has front matter, one H1 and a `Status:` line; ADR-0030 to ADR-0037, the three audit documents and every page a phase promised (`reference/cowork-projects-memory.md`, `reference/cowork-extensions.md`, `reference/cowork-safety.md`, `runbooks/cowork-isolation.md`) are listed too (the docs gate rejects an unlisted page). (AG-77-5; index s.4.1)
- **D-09:** Each runbook states: what it does, how to enable it, a table of every option variable, how to verify, the degraded states with their user text, owner actions, what cannot be verified. A test fails when a `Dispatch__`, `Accounts__`, `DataPlane__` or `Cowork__` variable of `.env.example` is missing from a runbook table. (AG-77-6)
- **D-10:** `README.md`, `docs/architecture.md`, `docs/guides/{status-and-roadmap,desktop-app-guide,web-app-guide,settings-guide,troubleshooting-and-faq,glossary,concepts}.md`, `docs/reference/api.md` and `docs/settings-parity.md` describe the Cowork surface, accounts, the data plane and Dispatch with their real status (off by default, nothing run live); `docs/architecture.md` has one row per `src/*` and `services/*` project and a test fails on a project directory without a row; no feature is described as working that only has fakes behind it, and each claim cites a parity row status. (AG-77-7; brief 77 U2)

### Parity audit (U3)
- **D-11:** `docs/parity/cowork-parity.md` is in the format of `docs/quality/cursor-parity-audit-2026-10-28.md` (front matter, status vocabulary, counts generated by a script, one table per family: Row, Feature, Status, Evidence, Gap or blocker) with rows C1 to C31 and K01 to K20 exactly once; it pins parity to capabilities, not labels. (ADR-0037 D14; AG-77-8)
- **D-12:** The audit test fails on: an empty cell; a Status outside MET, MET-LIVE, PARTIAL, DEFERRED, NOT-STARTED, OMITTED-BY-DECISION; an empty Evidence cell; an empty Gap cell on a row that is not MET; a MET row whose Evidence names no test class that `grep` finds under `tests/`; a MET-LIVE row without a path to a recorded run; a counts block that differs from the counts regenerated by `scripts/docs/parity_counts.py --check` (standard library only, wired into `scripts/ci/check-docs.sh`). (AG-77-8)
- **D-13:** `tests/Autonogrammer.ArchitectureTests/CoworkAuditTests.cs` carries fixture cases, one bad file per failure above, and passes on the real file, so a row cannot be graded by hand against the script; it also covers the variable-coverage and project-row checks of D-09 and D-10 and reads the security findings table (D-15). (AG-77-9, 6, 7, 11)
- **D-14:** Grading rule (U3): a row is MET only with a named test class found by `grep`; Windows, macOS, live and screen-reader rows stay PARTIAL or DEFERRED with the blocker; a row whose test class is not found is PARTIAL; no MET-LIVE row exists because OD-07 is not approved. (brief 77 U3, Failure states)

### Security review and accessibility pass (U4, U5)
- **D-15:** `docs/quality/v0.16.0-security-review.md` has a scope table (one row per area of the threat model, with result and evidence) and a findings table (id, phase, area, severity Critical/High/Medium/Low/Info, finding, evidence file and test, status open/fixed/accepted, owner action). Areas: Accounts tokens and keyring (67); data-plane tenancy and the no-RLS stance (68); artifact sandbox and CSP (71); browser and computer-use policies (72, 73); Dispatch relay keys and approvals (74); cross-cutting (dependency audit through `check-vulnerable-packages.py`, `secret-scan.sh`, `.env.example` holds no value, every endpoint in OpenAPI with its scheme, features default off, Web references no process-spawning project). It is a read of code and tests, not a penetration test. (AG-77-10; brief 77 Threat model)
- **D-16:** No Critical or High finding is `open` or `accepted`: it is `fixed` with a named regression test; a Medium or Low `accepted` needs an OQ id (OQ-482 default: yes with an OQ row and a named owner, High never). A High with no fix in reach means its phase is not merged and this phase stops. (AG-77-11; brief 77 Failure states)
- **D-17:** `docs/quality/v0.16.0-accessibility-pass.md` is a matrix of surface by check, every cell a result with the test or file that shows it, or "cannot be checked here" with the reason. Surfaces: Cowork shell and surface switch, account menu and sign-in panel, dock tab strip, approval prompt, artifact panel, Devices and Dispatch views. Checks: accessible name and role, keyboard path without trap and visible focus, focus returns to the opener, status text in aria-live (assertive for failure), contrast through theme tokens (no colour literal), reduced motion, 44 px touch target on Android. Missing automated checks are added as tests; screen readers (NVDA, VoiceOver, TalkBack, Orca) are listed as not checked (OQ-483). (AG-77-13)
- **D-18:** With the shipped default configuration the API boots and `Dispatch`, Cowork and Accounts routes answer their disabled problem type (`dispatch_disabled`, `cowork_disabled`, accounts disabled) or 404; each options validator refuses the unsafe values its brief names (in-memory presence with two replicas, a missing signing key with the feature on, a file key store); one integration test per module. (AG-77-12)

### Gate, integration and PR (U6, U7, U8)
- **D-19:** `./bootstrap.sh` exits 0 on the final integrated tree; the command, date, commit and counts go into `docs/runbooks/evidence/v0.16.0-gate-<date>/README.md`; a Live-category test is skipped with its stated reason, never run and never counted as passed; fix only what the gate finds, with a test, and report the phase and commit that broke a file this phase did not write. (AG-77-14; brief 77 U6 and Failure states)
- **D-20:** Integration (U7) follows rebrand runbook s.3 and s.4: replay, not merge (OQ-484): `git fetch`, fresh branch from the new base plus the feature commits dropping old rename commits, `dry-run`, read suspects, collisions and new tokens, `apply`, regenerate in order (relock with `--force-evaluate`, `dotnet format`, OpenAPI by building the API, notices, brand assets), `verify --baseline`, probes. Locks, OpenAPI and notices regenerate and are never merged by hand; `CHANGELOG.md`, `docs/open-questions.md`, `docs/SUMMARY.md` and `docs/decisions/README.md` merge by hand with ids renumbered only for the newer side; ADR numbers, OQ ids, migration numbers (M0001 and up) and `ContractVersion` have no duplicate or fork. A tool collision or `verify` mismatch stops the phase: fix the config, never the tool or the output. (AG-77-15; brief 77 U7)
- **D-21:** One PR (or the repo's stacked equivalent) from `.github/PULL_REQUEST_TEMPLATE.md` completed (gate, tests, docs, CHANGELOG, OQ rows, DCO sign-off, light and dark screenshots for UI), CI green on every required check, every review thread answered; the description states what was not verified; merge only on the owner's instruction; the Greptile loop only if a Greptile review appears (OQ-485; no Greptile configuration was found in the tree). (AG-77-16; brief 77 U8)
- **D-22:** No tag until the owner has done the actions that gate sign-in (OQ-480 default); the `release` workflow reads the CHANGELOG section on a `v*` tag and the repo has no tags. (brief 77 OQ-480, owner action 4; `.github/workflows/release.yml`)

### Legal admission and owner actions (U9)
- **D-23:** The vendored `mermaid.min.js` 12.2.0, which embeds `elkjs` (EPL-2.0) and unaudited d3 and other libraries, is an owner and legal decision (OQ-400, status open): the security review states "licence open"; the default of the row is to admit one unmodified vendored file, enumerate its libraries before commit and extend `scripts/legal`, and if refused to ship the Mermaid kind as source view. This phase decides nothing and changes `scripts/legal/*` only if the owner admits it. (brief 77 Cannot be verified and owner action 4; `docs/open-questions.md:269`)
- **D-24:** `docs/runbooks/v0.16.0-owner-actions.md` lists every owner action in the ADR-0030 table form (action and place, needed for, what the product does when it is not done) and the PR performs none. Groups: 1 Supabase and the website (O1 redirect URLs, O3 `rate_limit_email_sent`, O4 `email_data.token` in the `ConfirmSignup` template, publishable key as a Railway variable, `auth-doctor.mjs` before and after, first live check of OQ-430); 2 Railway (unpause deploys, create the API service and the `autonogrammer-migrate` one-shot `dotnet Autonogrammer.Api.dll migrate`, `DataPlane__*`, two P-256 keys in `Dispatch__CommandSigning__KeyPem` and `Dispatch__Credentials__SigningKeyPem`, `Dispatch__Enabled` only after sign-in works, OQ-420 and OQ-73, recreate or migrate state under old names); 3 renames (rebrand runbook s.5); 4 decisions (OQ-400, OQ-362, OQ-343 merged with OQ-402, tag `v0.16.0`, approval of a live omp turn OD-07). (AG-77-17; brief 77 Owner actions)

### Claude's Discretion
- Layout and wording of the six runbooks beyond the required parts; the row order inside the parity families; the fixture file names of `CoworkAuditTests`.
- Whether the AG-77-12 and AG-77-13 tests sit in `Api.IntegrationTests`, `Presentation.Tests`, `UI.Tests` or `Native.Tests` (brief lists all four).
- How the U7 replay is split into commits, provided locks, OpenAPI and notices are regenerated.

</decisions>

<specifics>
## Specific Ideas

- The Cursor audit permits a status word with a bracketed qualifier and counts the first word (`docs/quality/cursor-parity-audit-2026-10-28.md:43`); `parity_counts.py` and the test must parse the same way.
- Success criteria commands: `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*CoworkAudit*"`; `python3 scripts/docs/parity_counts.py --check`; `bash scripts/ci/check-docs.sh`; `bash scripts/ci/check-changelog.sh`; `dotnet restore Autonogrammer.Linux.slnf --locked-mode` (ROADMAP:1504-1508).
- OQ-481: the `AUTONOGRAMMER_RELEASE` docs gate (pinned screenshots) is not required for 0.16.0 by default; screenshots of new surfaces are added where a private Xvfb can make them.
- ADR-0037 D2 gives target "n/a" to covered rows (C1, C14, C16 to C22, C25, C29, K01 to K03, K05, K06, K07 to K09, K13, K14) and "n/a" to C31 (a scope statement), but AG-77-8 allows only the six statuses and forbids an empty cell; the unit that grades them (U3, rows created by 75-06) must give each a vocabulary status with test evidence, or OMITTED-BY-DECISION where ADR-0033 omits it (C20, C21) [INFERENCE: the documents do not say which].

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Decision records and brief
- `docs/plans/v0.16.0-research/briefs/77-hardening-and-integration.md` - requirements, units, threat model, owner actions, OQ-480 to OQ-485.
- `docs/decisions/ADR-0037-cowork-parity-remainder.md` D2 (rows C1 to C31, K01 to K20 and their targets), D14 (audit and registry ownership); `docs/decisions/ADR-0030-accounts-supabase-passwordless.md` D1 (owner-action table form, O1 to O7); ADR-0031 to ADR-0036 in `docs/decisions/` (what is integrated and audited: data plane, Cowork shell, Artifacts, Browser Use, Computer Use, Dispatch).
- `docs/plans/v0.16.0-research/briefs/00-index.md` (R1 to R18, row 77, OQ renumbering), `00-scaffold-manifest.md`, `docs/plans/v0.16.0-research/09-project-recipe-and-conventions.md` s.2 (guards, `VersionPrefix` relock), s.5 (the gate), s.7 and D2.
- `docs/runbooks/rebrand-autonogrammer.md` s.3 to s.5 (replay, regenerate order, owner actions); `.planning/ROADMAP.md:1497-1509` and `.planning/REQUIREMENTS.md` (AG-77 block).

### Files this phase touches (verified on disk unless marked)
- Version: `Directory.Build.props:8`, `deploy/api.Containerfile:102`, `CHANGELOG.md`, `scripts/ci/check-changelog.sh`, `.github/workflows/release.yml`.
- Docs and gates: `docs/SUMMARY.md`, `docs/architecture.md`, `docs/guides/*`, `docs/reference/api.md`, `docs/settings-parity.md`, `docs/decisions/README.md`, `scripts/docs/check_gitbook.py`, `scripts/ci/check-docs.sh`, `scripts/ci/repo-checks.sh`, `.github/PULL_REQUEST_TEMPLATE.md`, `bootstrap.sh`, `.env.example`.
- Tests: `tests/Autonogrammer.ArchitectureTests` (`DecisionRecordTests`, `RepoHygieneTests`, `WebShellAccessibilityTests`, `HeadMotionTests`), `tests/Autonogrammer.Native.Tests/ThemeContrastTests.cs`, `Api.IntegrationTests`, `Presentation.Tests`, `UI.Tests`, `Native.Tests`, `scripts/web-smoke` (axe).
- Created by earlier phases (do not exist yet): `docs/parity/cowork-parity.md` (75-06, amended 76-06); the runbooks and reference pages of D-08; the `Dispatch__`, `Accounts__`, `DataPlane__`, `Cowork__` rows of `.env.example` (0 today).
- Created by this phase: `scripts/docs/parity_counts.py`, `tests/Autonogrammer.ArchitectureTests/CoworkAuditTests.cs` and fixtures, `docs/quality/v0.16.0-security-review.md`, `docs/quality/v0.16.0-accessibility-pass.md`, `docs/runbooks/evidence/v0.16.0-gate-<date>/README.md`, `docs/runbooks/v0.16.0-owner-actions.md`.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `docs/quality/cursor-parity-audit-2026-10-28.md` (status vocabulary at :32-41, summary counts at :45-49) is the model for the parity file; `docs/parity/session-chat-parity.md` already sits in `docs/parity/`.
- `scripts/docs/check_gitbook.py` enforces front matter, quoted `description:`, one H1, a `Status:` line within 15 lines of the H1 (`:60`, `:204`) and the no-orphan rule against `docs/SUMMARY.md`.
- `ProjectGraphChecker.FindRepoRoot` is how architecture tests read repository files (`RepoHygieneTests.cs:25`), the pattern for `CoworkAuditTests`.

### Established Patterns
- Counts in audits are produced by a script, never typed; evidence pages live under `docs/runbooks/evidence/<name>-<date>/README.md`.
- Live tests are trait-gated and skipped with a reason in the gate (`-- --filter-not-trait "Category=Live"`, `bootstrap.sh`).
- Decision records need the headings `Candidate chosen`, `Licence`, `Verification route`, `Open risks` and one row each in `docs/decisions/README.md` (`DecisionRecordTests.cs:43,107`).

### Integration Points
- Everything of phases 66 to 76 meets here: the parity file's evidence cells name test classes of every phase; the variable-coverage test reads `.env.example` against the runbooks; the version bump relocks the 40 scaffold projects too.
- Index row 77 and ROADMAP:1500 depend on 66 being applied (U1 commit `4af7d834` exists; regenerate, migrations and gate are 66's remaining units).

</code_context>

<deferred>
## Deferred Ideas

- Any live run (Supabase, Railway, a real omp turn), Windows and macOS runners, Android build and a real phone, real screen-reader passes, WebView2 and WKWebView, a pinned Chrome for Testing: not done here; rows stay PARTIAL or DEFERRED, MET-LIVE is empty (OD-07).
- Release tag `v0.16.0` and publishing (OQ-480, default no tag yet); external renames and `apply --urls` (owner actions); a third-party penetration test and an assistive-technology audit (OMITTED-BY-DECISION here: the review is a read of code and tests).
- Open questions and defaults: OQ-480 no tag until owner actions are done (owner); OQ-481 screenshots gate not required (owner); OQ-482 Medium accepted with OQ row, High never (owner, security); OQ-483 screen-reader pass listed as not checked (owner); OQ-484 replay (architect); OQ-485 Greptile loop only if a review appears (owner); OQ-400 Mermaid admission open (product owner and legal); OQ-362, OQ-343, OQ-420, OQ-73, OQ-430 are owner actions of D-24.

</deferred>

---

*Phase: 77-hardening-docs-and-integration*
*Context gathered: 2026-10-28*
