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

## Workflows profile

The Temporal dev server (needed only when `Workflows:Enabled` is true, Phase 3+) runs under podman-compose:

```sh
podman-compose --profile workflows up
```

This is the only supported invocation. The API, worker, and web runbooks land with their phases:

- API backend serving and SSE resume: added in Phase 2.
- Blazor web head serving: added in Phase 4.
- MAUI head builds: macOS CI only, never locally on Linux.

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
export DOTNET_ROOT=$HOME/.dotnet Harness__Adapter=InMemory Api__Cors__Origins__0=http://127.0.0.1:5180
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
2. Click "Start new conversation", type a prompt into "Message", click "Send message".
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

### Accessibility run (UI-03)

axe-core 4.14.0 (WCAG 2.0 and 2.1, levels A and AA) against the published Release head, run in an owned headless Chrome on 2026-10-21:

- Empty state and an active conversation with a streamed reply: **0 violations** in both states.
- First-run findings, fixed: no skip link (`bypass`), and the accessible name of the New button did not contain its visible label (`label-content-name-mismatch`). The skip link is shared by both heads through `SessionView`.
- Remaining `incomplete` items, both on the icon-only send control: axe cannot evaluate a pictogram's label or colour automatically. Measured in the live page: foreground `rgb(0, 0, 0)` on `rgb(239, 239, 239)`, contrast **18.26:1** (WCAG 1.4.11 requires 3:1; text requires 4.5:1).
- Not isolated: one screenshot captured immediately after the full axe run showed the skip link. Probes before, during and after a send, a stream, and an axe run show it hidden (1 px, clipped, unfocused), and a later screenshot in the same state does not show it. The cause was not isolated; the product path keeps the link hidden.
- Not verified here: the MAUI head (it cannot run on Linux) and assistive-technology testing with a screen reader.

### Vercel static deploy (WEB-02)

`vercel.json` at the repo root publishes the WASM head statically: the SDK installs via `dotnet-install.sh --jsonfile global.json` into `./.dotnet`, the build runs `./.dotnet/dotnet publish apps/OmpOS.Web -c Release -o output`, and `output/wwwroot` is served with an SPA rewrite to `/index.html` plus security headers.

The CSP is `connect-src 'self' http://127.0.0.1:5187` and `img-src 'self' data:`. Before deploying, add the production API origin to `connect-src`: the static deploy cannot reach any other API. The browser never fetches remote images. `script-src 'self' 'wasm-unsafe-eval'` is required for Blazor WASM. Never add a legacy `routes` key alongside `rewrites`/`headers`.

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
