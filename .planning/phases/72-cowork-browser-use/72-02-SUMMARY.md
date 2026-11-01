---
phase: 72-cowork-browser-use
plan: 02
subsystem: browser
tags: [egress, ssrf, navigation-gate, proxy]
requires:
  - phase: 72-cowork-browser-use
    provides: BrowserUseOptions PolicyMode, AllowedHosts, DeniedHosts
provides:
  - Session loopback egress guard with self-resolution and pinned dial
  - Address classifier over private ranges and ambiguous literals
  - Origin grants, URL policy, and document NavigationGate
affects: [72-cowork-browser-use]
actuals:
  tokens: 28000
  tasks: 3
  commits: 1
tech-stack:
  added: []
  patterns: [pinned dial, fetch document pause, session local-origin grant]
key-files:
  created:
    - src/Autonogrammer.Harness.BrowserUse/Egress/EgressGuardProxy.cs
    - src/Autonogrammer.Harness.BrowserUse/Egress/AddressClassifier.cs
    - src/Autonogrammer.Harness.BrowserUse/Policy/NavigationGate.cs
    - tests/Autonogrammer.Harness.BrowserUse.Tests/Support/LocalTestSite.cs
  modified: []
key-decisions:
  - "A literal with an octal reading and a different decimal reading is ambiguous and refused, including when the octal reading is public."
  - "Loopback is dialed only when a session LocalOriginGrant names localhost, 127.0.0.1, or [::1] and the port."
  - "PromptOnNewOrigin does not card subresource loads. That residual is named in the test and left for Strict."
patterns-established:
  - "PinnedDial resolves a name once and the connector receives an IPEndPoint."
  - "NavigationGate enables Fetch with resourceType Document and fails denied hops with BlockedByClient."
requirements-completed: [AG-72-4, AG-72-5]
coverage:
  - id: D1
    description: Classifier denies private, link-local, metadata, mapped, and NAT64 forms and refuses multi-reading literals.
    requirement: AG-72-4
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.BrowserUse.Tests/Egress/AddressClassifierTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: A flipped second resolver answer is not used. The dial pins the first validated address.
    requirement: AG-72-4
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.BrowserUse.Tests/Egress/RebindingTests.cs
        status: pass
    human_judgment: false
  - id: D3
    description: Proxy forwards HTTP and CONNECT to a granted port, refuses the ungranted port, wrong credentials, a down guard, and unlisted Strict hosts.
    requirement: AG-72-4
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.BrowserUse.Tests/Egress/EgressProxyTests.cs
        status: pass
    human_judgment: false
  - id: D4
    description: Document redirects to an ungranted port, metadata, and RFC1918 produce zero target hits. Stage-2 cards omit the query.
    requirement: AG-72-5
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.BrowserUse.Tests/Policy/NavigationGateTests.cs
        status: pass
    human_judgment: false
duration: 90min
completed: 2026-10-30
status: complete
---

# Phase 72 Plan 02 Summary

**Loopback egress guard, address pinning, origin grants, and document navigation gate**

## Performance

- **Duration:** 90 min
- **Started:** 2026-10-30
- **Completed:** 2026-10-30
- **Tasks:** 3
- **Files modified:** 22

## Accomplishments

- `AddressClassifier` denies loopback, RFC1918, link-local including 169.254.169.254, CGNAT, unspecified, multicast, reserved, and IPv6 unique-local and link-local. Mapped and NAT64 forms are unwrapped and re-checked. Multi-reading literals are refused.
- `EgressGuardProxy` is a session loopback HTTP and CONNECT proxy with per-session credentials. It fails closed with `EgressGuardUnavailable` when it is not running.
- `NavigationGate` pauses document requests, including redirect hops, and aborts denied targets before the scripted session records a hit.

## Task Commits

1. **Task 1-3: classifier, proxy, gate** - one commit (feat)

## Files Created/Modified

- `src/Autonogrammer.Harness.BrowserUse/Egress/**` - classifier, resolver, pinned dial, proxy
- `src/Autonogrammer.Harness.BrowserUse/Policy/**` - grants, URL policy, navigation gate
- `tests/Autonogrammer.Harness.BrowserUse.Tests/Support/LocalTestSite.cs` - two loopback ports and a redirect endpoint
- `tests/Autonogrammer.Harness.BrowserUse.Tests/Egress/**` and `Policy/NavigationGateTests.cs` - offline proofs

## Decisions Made

- Leading-zero literals whose octal and decimal readings differ are ambiguous, so a public octal reading cannot bypass a private decimal reading.
- Local servers require an explicit session grant. Always is refused.
- Chromium `Page.AuthenticateAsync` wiring is [INFERENCE] and is not asserted.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Extra types under owned folders**
- **Found during:** Task 1
- **Issue:** MA0048 requires one type per file.
- **Fix:** Split classification, connector, dial, and exception types under Egress and Policy.
- **Files modified:** Egress and Policy folders
- **Verification:** Release build of the test project is warning-free
- **Committed in:** the plan commit

---

**Total deviations:** 1 auto-fixed
**Impact on plan:** Required by the one-type-per-file rule. No new package.

## Issues Encountered

- The live redirect-zero-hit proof stays in 72-07. This plan did not launch Chromium or Xvfb.
- Subresource loads in PromptOnNewOrigin do not raise cards. The test name records that accepted residual.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Later plans can start `EgressGuardProxy` and attach `NavigationGate` to an `ICDPSession`.
- Proxy credentials are generated per instance. Wiring them into Chromium is still [INFERENCE].

---
*Phase: 72-cowork-browser-use*
*Completed: 2026-10-30*
