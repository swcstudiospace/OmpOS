# Local development runbook

## Bootstrap

From a clean clone, one command does everything:

```sh
./bootstrap.sh
```

It installs the .NET SDK pinned in `global.json` into `~/.dotnet` when missing, then runs a locked restore, a warnings-as-errors Release build, the test suite, and a format verification. Any failing step exits non-zero.

Every shell that calls `dotnet` directly needs the user SDK first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH
```

## Local dependency stack

The Temporal dev server and its `workflows` profile were removed on 2026-10-21 ([decision](../handoff.md#temporal-removal-and-railway-topology-2026-10-21)). Session runs execute in-process in the API, so no local Temporal server is needed. `podman-compose --profile workflows` no longer applies.

The API and web runbook sections land with their phases:

- API backend serving and SSE resume: added in Phase 2.
- Blazor web head serving: added in Phase 4.
- Native head builds: the Avalonia UI native heads (ADR-0004) are not in the repo yet. Supersession note (2026-10-21): the MAUI head build in the CI `shells` job (macOS only, Android target, run only when `apps/OmpOS.App/OmpOS.App.csproj` exists) is superseded and goes with the MAUI head at v0.10.0 Phase 25. Never built locally on Linux.

## Troubleshooting

- `error NU1004 ... pass an explicit --force-evaluate option`: the lock file drifted after a dependency change. Relock with `dotnet restore OmpOS.Linux.slnf --force-evaluate`. Note `CI=true` in the environment enables locked mode locally, so this also happens on the dev box.
- `error NU1510 ... will not be pruned`: a `PackageReference` names a package already in the shared framework (e.g. `System.Text.Json`). Remove the reference instead of pinning it.
- `error IDE0005` (unused using) or `error WHITESPACE`: run `dotnet format OmpOS.Linux.slnf` and commit the result.
- `podman-compose: failed to check if podman is installed` (or `command not found`): install podman, then `pipx install podman-compose==1.6.0`. Compose validation runs in CI; locally it is optional.
- `dotnet: command not found` after bootstrap: the current shell lacks the `DOTNET_ROOT` export above.
- Wrong SDK version (`dotnet --version` is not `10.0.402`): delete the stray SDK from `PATH` or unset a competing `DOTNET_ROOT`; `global.json` pins the exact version with `latestPatch` roll-forward.

## Vertical slice: web head against the API (WEB-01)

The Blazor WASM head (`apps/OmpOS.Web`) hosts the shared `OmpOS.UI` chat surface and
talks to the API over HTTP + SSE. No host is hardcoded: the API origin comes from
`apps/OmpOS.Web/wwwroot/appsettings.json` (`Api:BaseUrl`, default
`http://127.0.0.1:5187`) and can be overridden per boot with
`?apiBaseUrl=https://api-host` in the page URL.

### Serve the API (in-memory adapter)

```sh
export DOTNET_ROOT=$HOME/.dotnet ASPNETCORE_ENVIRONMENT=Development Sessions__Store=InMemory Coordination__Store=InMemory Api__Auth__AllowDevelopmentPrincipal=true Harness__Adapter=InMemory Api__Cors__Origins__0=http://127.0.0.1:5180
dotnet run --project services/OmpOS.Api --urls http://127.0.0.1:5187
```

`Api:Cors:Origins` is empty by default, which denies every cross-origin request. Set it to the origin that serves the web head (the smoke uses `http://127.0.0.1:5180`).

### Serve the published web head

```sh
dotnet publish apps/OmpOS.Web -c Release --no-restore -o /tmp/ompos-web
python3 -m http.server 5180 --bind 127.0.0.1 --directory /tmp/ompos-web/wwwroot
```

The page's default API origin is `http://127.0.0.1:5187` (`apps/OmpOS.Web/wwwroot/appsettings.json`).

### Browser smoke (executed 2026-10-21)

Run it in an owned, empty-profile headless Chrome, never a user profile. Launch the Playwright-managed `chrome-linux64/chrome` with `--headless --no-sandbox --disable-dev-shm-usage --remote-debugging-port=0` and a fresh `--user-data-dir`, then attach with `browser.open({ app: { cdp_url } })`.

1. Load `http://127.0.0.1:5180/`. Expect the conversation list, no unhandled-error bar and no console errors.
2. Click "+ New", type a prompt into "Message", click "Send message".
3. Expect the user turn as escaped text, the assistant output streamed to "Response complete.", bold markdown rendered, and remote images shown as links with no `img` element.
4. Reload and open the conversation. Expect the stored history to render the same way.
5. Screenshot: `docs/runbooks/evidence/web-vertical-slice-smoke.webp`.

Probe prompt: `**bold** and [site](https://example.com/) and ![pic](https://evil.example/x.png) and <script>alert(1)</script>`.
Observed across load, send and reload: `strong` count 1, `img` count 0, `#app script` count 0, no "Unsupported content", no error bar, no console errors after load.

#### Defects this smoke found, all fixed

- Crash on first render with `JsonSerializerIsReflectionDisabled`: the repo-wide reflection switch in `Directory.Build.props` broke Blazor's `HeadOutlet` JS interop. The two Blazor heads re-enable it (open question Q-WEB-REFLECTION). The MAUI head override is inferred, not built on Linux.
- "Sorry, there is nothing at this address" on `/`: the trimmer removed `Pages/Index.razor`, because the router discovers pages by reflection. `OmpOS.Web` roots its assembly with `TrimmerRootAssembly`.
- Unstyled page: `_content/OmpOS.UI/styles/ompos.css` shipped but was never linked.
- `prompt_accepted` rendered as "Unsupported content", and the user's turn never appeared. It is now a user-role message.
- `NoMetadataForType` on reload once a session had streamed events: the web client decoded contract types with a context that lacked the payload types. Contract types now decode through `OmpOsJsonContext`.
- Markdown rendered as plain text, and the copy control was unbound. Markdig now renders with raw HTML disabled and a link scheme allowlist.

> Supersession note (2026-10-21, ADR-0004): the MAUI head named in the first item above is superseded and retires by clean cutover after parity tests (v0.10.0 Phase 25). Its reflection override was never built on Linux. The native heads are Avalonia UI, and this record does not establish their reflection setting.

### Accessibility run (UI-03)

axe-core 4.14.0 (WCAG 2.0 and 2.1, levels A and AA) against the published Release head, run in an owned headless Chrome on 2026-10-21:

- Empty state and an active conversation with a streamed reply: **0 violations** in both states.
- First-run findings, fixed: no skip link (`bypass`), and the accessible name of the New button did not contain its visible label (`label-content-name-mismatch`). The skip link is shared by both heads through `SessionView`.
- Remaining `incomplete` items: two, both on the icon-only send control (`button.ompos-icon-btn`, `src/OmpOS.UI/Components/Composer.razor`). Axe reports `incomplete` when its automatic check cannot reach a decision. The 2026-10-21 record did not keep the rule IDs or node targets of the two items, so the rule behind each one is not identified here. Measured in the live page, the glyph colours are foreground `rgb(0, 0, 0)` on `rgb(239, 239, 239)`, contrast **18.26:1** (WCAG 1.4.11 requires 3:1; text requires 4.5:1). The record's statement that the pictogram's label cannot be evaluated does not hold for the final tree: the send button carries `aria-label="Send message"` (send button markup in `Composer.razor`), pinned by `tests/OmpOS.Web.Tests/ComposerBusyTests.cs` (`button[aria-label='Send message']`). Fix: the accessible name is in place and matches the copy the smoke uses; no markup change was needed. Status: to be re-run on the final tree.
- Skip-link screenshot: not explained by the record. The only screenshot in the repo, `docs/runbooks/evidence/web-vertical-slice-smoke.webp`, comes from the browser smoke, not the axe run, and shows no skip link. The axe-run screenshot that showed the link was not retained, so its cause cannot be established from the repo. The stylesheet clips `.ompos-skip-link` to 1 px and expands it only under `:focus` (`src/OmpOS.UI/wwwroot/styles/ompos.css`, the `.ompos-skip-link` and `.ompos-skip-link:focus` rules; the link is at `src/OmpOS.UI/Components/SessionView.razor:7`). [INFERENCE] The link becomes visible in the repo's CSS only while it holds keyboard focus; the record does not say whether focus was on it. Status: to be re-run on the final tree; the re-run must record the link's state with each screenshot (step 6 below).
- Not verified here: the MAUI head (it cannot run on Linux) and assistive-technology testing with a screen reader.

> Supersession note (2026-10-21, ADR-0004): the MAUI head and the "both heads" wording in the accessibility record above refer to the .NET MAUI Blazor Hybrid head, which is superseded and retires by clean cutover after parity tests (v0.10.0 Phase 25). The native heads are Avalonia UI, and this record does not cover them. The web head results above are unchanged.

#### Re-run of axe-core on the final tree

Status: re-run on commit 924ca78 (2026-10-21). Result below.

1. Set the SDK and publish the Release web head: `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH`, then `git rev-parse --short HEAD` (record it), then `dotnet publish apps/OmpOS.Web -c Release --no-restore -o /tmp/ompos-web`.
2. Serve the API and the web head exactly as in "Serve the API (in-memory adapter)" and "Serve the published web head" above, so the page is at `http://127.0.0.1:5180/`.
3. Fetch the pinned engine: `mkdir -p /tmp/axe && cd /tmp/axe && npm pack axe-core@4.14.0 && tar -xzf axe-core-4.14.0.tgz package/axe.min.js`.
4. Launch the owned headless Chrome and attach over CDP as in "Browser smoke" (fresh `--user-data-dir`, never a user profile), then load `http://127.0.0.1:5180/`.
5. Inject `/tmp/axe/package/axe.min.js` into the page, then run `axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] }, resultTypes: ['violations', 'incomplete'] })`. Record each `violations` and `incomplete` entry as `id`, `impact`, and every `nodes[].target`. Run it once in the empty state, then again after the probe prompt from the smoke has streamed to "Response complete.".
6. Before and after each axe run, record the skip link (`.ompos-skip-link`): `getBoundingClientRect()`, computed `clip`, `width`, and `document.activeElement`. Take a screenshot at each point and keep it with the JSON.
7. For each `incomplete` node on `button.ompos-icon-btn`, record the rule ID and the computed `color` and `background-color` of the node, and compute the contrast ratio.
8. Save the axe JSON and screenshots under `docs/runbooks/evidence/` with the commit in the file name, then add a dated entry here with the commit, the counts of violations and incomplete items, the rule IDs of the two items, the skip-link probe results, and the new status.

**Dated entry, 2026-10-21, commit 924ca78.** Browser smoke (`docs/runbooks/evidence/web-smoke-924ca78-2026-10-21.json`, `.log`, and `web-first-render-924ca78-2026-10-21.png`, `web-axe-empty-924ca78-2026-10-21.png`, `web-axe-after-response-924ca78-2026-10-21.png`): first render is clean with `JsonSerializerIsReflectionEnabledByDefault` at its scoped `true`. The switch-off run failed the first render (`web-reflection-off-2026-10-21.json`), so the override stays. The probe streamed to "Response complete." with `strong` 1, `img` 0 and `script` 0 under `#app`, and no error bar.

axe-core 4.14.0, WCAG 2.0 and 2.1, levels A and AA: 0 violations and 0 `incomplete` items in the empty state and after the streamed response. The two earlier `incomplete` items on `button.ompos-icon-btn` (`color-contrast` and `label-content-name-mismatch`) are resolved. The glyph is now an `aria-hidden` SVG, the accessible name stays `Send message`, and the measured contrast is 18.26:1.

Skip link: on a fresh load, one Tab from the body focuses `a.ompos-skip-link`, and its target `#ompos-conversation` exists (`web-skip-link-focus-924ca78-2026-10-21.json`, `web-skip-link-focused-924ca78-2026-10-21.png`). The link is clipped to 1 px until it has focus. The earlier skip-link screenshot was taken with focus on the link, which explains it. The earlier probe that read focus after a Send click is not a skip-link result, because the Tab started from that button.

Not verified here: the MAUI head (it cannot run on Linux) and screen-reader testing.

> Supersession note (2026-10-21, ADR-0004): the MAUI head referenced in this line is superseded, as described in the note under the accessibility run above.

### Vercel static deploy (WEB-02)

`vercel.ts` at the repo root is the only Vercel configuration (the static `vercel.json` was removed on 2026-10-21, because Vercel reads static config before the build runs). It executes at build time, so the CSP is generated from one validated API origin. The SDK installs via `dotnet-install.sh --jsonfile global.json` into `./.dotnet`, the build runs `./.dotnet/dotnet publish apps/OmpOS.Web -c Release -o output`, and `output/wwwroot` is served with an SPA rewrite to `/index.html` plus security headers.

`connect-src` comes from `scripts/vercel/render-csp.sh`, which reads `OMPOS_API_ORIGIN` and accepts only `https://host[:port]`: no path, query, fragment, userinfo, wildcard, trailing slash or IP-literal brackets. An invalid or missing origin aborts config evaluation, so the deploy fails closed. Production must set `OMPOS_API_ORIGIN` in the Vercel project environment; preview builds need it too. For local checks only, `OMPOS_API_ORIGIN_ALLOW_LOOPBACK=1` also accepts `http://127.0.0.1:<port>`. `script-src 'self' 'wasm-unsafe-eval'` is required for Blazor WASM, and `img-src 'self' data:` means the browser never fetches remote images. Never add a legacy `routes` key alongside `rewrites`/`headers`.

Verified locally on 2026-10-21 by evaluating `vercel.ts` with Bun: a valid HTTPS origin produces `connect-src 'self' <origin>`; a wildcard, a missing origin, and loopback without the flag each exit 1. Not verified: config evaluation on Vercel itself (working directory and project environment visibility) needs a preview deployment, which also needs the production API origin.

### Clean-clone bootstrap (VERIFY-01)

Run on 2026-10-21 against commit `a6fcadd` (branch `feat/ompos-scaffold`, the committed HEAD of the gate), from a fresh clone into an empty temp directory, with an empty SDK root so the pinned SDK must be installed:

```sh
git clone /root/src/repos/ompos /tmp/ompos-clean
cd /tmp/ompos-clean
env -u DOTNET_ROOT DOTNET_ROOT=/tmp/ompos-clean-dotnet2 ./bootstrap.sh
```

- The bootstrap installed .NET SDK 10.0.402 into the empty root.
- Locked restore, a Release build with warnings as errors (0 warnings, 0 errors), the full test run (615 total, 0 failed) and the format verification all passed; the script printed `bootstrap: OK`.
- Log: `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21.log` (commit `a6fcadd`). An earlier clean run at `8479c7c` (614 total, 0 failed) is kept as `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-8479c7c.log`.
- Limits: the clone was taken from the local repository, not from the GitHub remote, which had not been pushed when this ran. The NuGet package cache was warm, so the run does not measure cold package download time. Host: linux 6.8.0-147-generic, x64.
- Re-run at the final code head `fc4f069` (after the carriage-return reader fix): same procedure, SDK root `/tmp/ompos-clean-dotnet4`, bootstrap OK, 706 total, 0 failed. Log: `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-fc4f069.log`. The earlier re-run at `642c1e5` (704 total) is kept as `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-642c1e5.log`.
- Re-run at the newest code head `bc3d7b4` (the ordered-stream engine-host contract and the SessionHost drain): same procedure, SDK root `/tmp/ompos-clean-dotnet5`, bootstrap OK, 706 total, 0 failed. Log: `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-bc3d7b4.log`.
- Re-run at the newest code head `180817d` (the stream owner lease, and disposal of the remote engine host): same procedure, SDK root `/tmp/ompos-clean-dotnet6`, bootstrap OK, 707 total, 0 failed. Log: `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-180817d.log`.
- Re-run at the newest code head `f77221a` (the lock-owned fetch lease that replaces the disposable one): same procedure, SDK root `/tmp/ompos-clean-dotnet7`, bootstrap OK, 707 total, 0 failed. Log: `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-f77221a.log`.
- Re-run at the v0.9.2 hardening head `2549586`, cloned from the pushed remote branch `origin/feat/v0.9.2-hardening` (not from a local path, as the earlier runs were): the bootstrap installed .NET SDK 10.0.402 into the clone, the locked restore, Release build (warnings as errors), 914 tests (914 passed, 0 failed, 0 skipped) and format verification passed; the script printed `bootstrap: OK` in 113 seconds. Host: linux 6.8.0-147-generic, x64. Log: `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-2549586.log`.
- Re-run at the final pushed head `dee19dd`, cloned from `origin/feat/v0.9.2-hardening`: bootstrap OK in 184 seconds, 925 tests (925 passed, 0 failed, 0 skipped), locked restore, Release build with warnings as errors, and format verification passed. Host: linux 6.8.0-147-generic, x64; SDK 10.0.402 installed by the bootstrap. Log: `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-dee19dd.log`.
- Re-run at HEAD `a26fe2f` (REC-02), cloned from the pushed remote branch `origin/feat/v0.9.2-hardening` into `/tmp/ompos-clean-a26fe2f`. Start 2026-10-21 19:15:07 UTC, end 19:17:43 UTC, 2 minutes 36 seconds. `./bootstrap.sh` exited 0 and printed `bootstrap: OK (SDK 10.0.402, locked restore, Release build, tests, format)`. Tests: 1080 total, 1080 passed, 0 failed, 0 skipped. Host: Ubuntu 24.04.4 LTS, kernel 6.8.0-147-generic, x64. Log: `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-a26fe2f.log`.
