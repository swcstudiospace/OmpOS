# Browser smoke evidence of 2026-11-19 (run-once pass kept in the repo)

Passing smoke JSONs kept as repo evidence (OQ-191's "keep passing smoke JSON"). Content downloaded from the CI artifact
`web-smoke-evidence-1` of run 44595229003 (PR #21's push twin, head `4a7f34c2` — the app tree of current main; the rerun
of the failed smoke job in that run and run 44595243597 are both green). All four smoke halves of that run exited 0
(`api smoke exit=0`, `size budget exit=0`, `demo smoke exit=0`, `artifact smoke exit=0`); the job failed only on a
Chrome-profile cleanup race (`rm: cannot remove .../chrome-profile-1/Default': Directory not empty`), unrelated to the app.

Files: `web-smoke-4a7f34c2-2026-11-19.json` (API half, `passed: true`), `web-smoke-demo-4a7f34c2-2026-11-19.json`
(Demo half, `passed: true`, 0 failures), `web-smoke-artifact-4a7f34c2-2026-11-19.json`,
`size-budget-4a7f34c2-2026-11-19.json`, and the two `a11y/{api,demo}/a11y-report.json` (0 error findings on both halves;
API half: 48 warnings, of which 36 are the `state-unreachable` optional states of known issues K18; Demo half: 12 warnings).

OQ-191 (first-load flake): first loads now succeed within the 240 s CI budget in every group
(counters `load:attempt:1` and `success:first-attempt` all 1 in the API report, no `app-loaded-on-retry` warnings).
The retry design stands as the accepted answer.
