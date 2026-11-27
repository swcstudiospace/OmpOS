# P4 rerun (2026-11-27) — Phase 66 SC3 web-smoke gate + Phase 66.3 SC1 smoke-axe gate

Linear: SPE-34890 (Phase 66 SC3), SPE-34960 (Phase 66.3 SC1). Both gates run the
same command shape (`scripts/web-smoke/run-smoke.sh`, default targets
`api demo artifact`); one execution covers both, and one verdict covers both.

## Verdict: BLOCKED — environment (host too slow for the 1500 s budget)

No product failure was observed. The gate never reached the browser assertions:
the build phase alone consumed the whole 1500 s timeout on this host, so no
axe-core counts (light/dark) could be produced. Two attempts, both exit 124
(same as `timeout(1)` when it kills the command). Nothing was written to
trackers/Linear; this is the report-only lane.

## What ran

Commit `41e9c968`, 2026-11-27. Host facts: .NET SDK 10.0.402, Chrome
153.0.8010.36 (`/usr/bin/google-chrome`), Node v24.18.0, 8 cores, load
average ~27 (a heavily shared host; earlier evidence on this host recorded
load averages of 20–36 with the same symptom).

Command (both attempts), run from the repo root:

```bash
OUT_DIR=docs/runbooks/evidence/p4-rerun-2026-11-27/smoke-out \
  timeout 1500 bash scripts/web-smoke/run-smoke.sh \
  --a11y-report docs/runbooks/evidence/p4-rerun-2026-11-27/smoke-out/a11y
```

Attempt 1 (default script knobs): **exit 124 after ~300 s**. The inner
`AUTONOGRAMMER_SMOKE_PUBLISH_TIMEOUT` default (300 s) killed the web-head
publish mid-build. `run.log` holds only the start banner; the publish log
ends mid-build with no compiler error — the process was killed by the timeout,
not by a compile failure:

```
commit=41e9c968 date=2026-11-27 chrome=/usr/bin/google-chrome targets=api demo artifact
  Autonogrammer.Harness.Secrets -> .../bin/Release/net10.0/Autonogrammer.Harness.Secrets.dll
```

Attempt 2 (documented loaded-host knobs, `AUTONOGRAMMER_SMOKE_PUBLISH_TIMEOUT=900`,
`AUTONOGRAMMER_SMOKE_DEMO_TIMEOUT=900`, still under the 1500 s budget):
**exit 124 at exactly 1502 s wall**. This attempt got through the entire build
phase — web publish, `finish-web-bundle.sh` (128 assets match the manifest),
API publish, Demo native relink ("Demo bundle written"), both static servers
up, and headless Chrome up with a DevTools port:

```
commit=41e9c968 date=2026-11-27 chrome=/usr/bin/google-chrome targets=api demo artifact
render-web-config: Api:BaseUrl set to http://127.0.0.1:5187 in /tmp/.../web/wwwroot/appsettings.json
refresh-web-manifest: manifest already matches, nothing changed
refresh-web-manifest: 128 assets match the manifest
check-no-demo: /tmp/.../web/wwwroot carries no Demo code
```

```
DevTools listening on ws://127.0.0.1:41735/devtools/browser/b8b7aedb-...
```

It then died inside the browser halves: no `web-smoke-*.json`,
`web-smoke-demo-summary-*.json` or `a11y-report.json` was ever written, so no
assertion ran and no axe error counts (light or dark) exist for this commit
on this host.

## Close-out facts

- Both builds are green (web, API, Demo relink); the failure is purely
  wall-clock on a loaded host — the same failure mode this host's earlier
  evidence documents (load averages 20–36, first-load flakes).
- The API's self-generated `docs/api/openapi.json` rewrite is byte-identical:
  `git status` shows no tracked-file change from either run.
- The gate as documented needs either a less-loaded host or a larger outer
  budget: build phase alone ≈ 1400 s here, before any browser assertion.
- No rerun was attempted after attempt 2: attempt 1 already proved the
  default knob is too small, attempt 2 proved the builds themselves fit and
  the browser halves are what the 1500 s budget cannot hold.

## Artifacts (this directory)

- `smoke-out/` — attempt-2 logs: `run.log`, `web-publish-41e9c968-2026-11-27.log`,
  `api-publish-41e9c968-2026-11-27.log`, `demo-build-41e9c968-2026-11-27.log`,
  `api-41e9c968-2026-11-27.log`, `chrome-41e9c968-2026-11-27-1.log`,
  `chrome-version-41e9c968-2026-11-27.txt`, `demo-static-*/web-static-*` logs.
- `attempt1/` — attempt-1 `run.log` and the killed-publish log.
- `66-sc3-smoke.md` — this note.

## Acceptance check

Exit code: **124 both attempts (timeout-killed, environment)**. Axe errors
light+dark: **not obtainable — browser halves never executed** (verbatim
blocker above). A passing rerun must be scheduled on an idle host or with a
budget that covers the ~1400 s build phase plus the browser halves.
