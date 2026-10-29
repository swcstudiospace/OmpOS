---
description: "Evidence of the local end-to-end run of the real web build against the Autonogrammer API and the real omp (scripted model), 2026-10-29: steps, results, defects the run found and what it does not prove."
---

# Hosted web app: local end-to-end run

Status: Recorded 2026-10-29 on a Linux x64 host. Nothing in this folder was produced on a deployment: the API, omp and the browser ran on one machine, on loopback.

Last verified: 2026-10-29

## What ran

`scripts/hosted/run-local.sh --web-bundle <dir>` started, in order:

1. a scripted OpenAI-compatible model server on `127.0.0.1:18080` (`scripts/hosted/fake-model-server.mjs`; no network egress, no credential);
2. the Release build of `services/Autonogrammer.Api` in owner-token mode (`Api:Auth:Mode=OwnerToken`, the digest of a generated token), with `Harness:Acp:Environment:Mode=Allowlist`, a per-session workspace and the approval recipe `Kinds` = execute and edit, `GrantableTools` = `*`;
3. the real `omp acp` (omp 18.12.6) that the API starts for each session, with an isolated agent directory (the real `~/.omp` is never read or written) that points at the scripted model;
4. the API checks (`e2e-api.mjs`) and, with a bundle, the browser checks (`e2e-browser.mjs`, headless Chrome over DevTools) against the **real web bundle**: `scripts/vercel/build-web.sh` with `AUTONOGRAMMER_API_ORIGIN=http://127.0.0.1:5187 AUTONOGRAMMER_API_ORIGIN_ALLOW_LOOPBACK=1`, which `check-no-demo.sh` had asserted free of Demo code.

## Result

All steps passed (run of 2026-10-29, 08:15 to 08:18 UTC; the code under test is the one of commit `849780ef`, plus a stylesheet change to the agent rail that no step depends on).

| Where | Step | Result | What it shows |
|-------|------|--------|---------------|
| API | auth-config | passed | `GET /auth/config` answers `owner-token` without a credential |
| API | unauthenticated | passed | a request without a token is refused with `WWW-Authenticate: Bearer` |
| API | bad-token | passed | a wrong token is refused the same way (no body, no hint) |
| API | text-turn | passed | a prompt becomes a session; the stream carries `prompt_accepted`, `text_delta`, `context_usage`, `completed` with consecutive sequence numbers |
| API | tool-turn | passed | the real omp raised `session/request_permission` for a `bash` call under `tools.approvalMode: always-ask`; the approval endpoints granted it; the tool ran and the reply was `done: 18` |
| API | resume | passed | a second connection with `Last-Event-ID` received the events after the last one it had seen |
| API | rate-limit (own run, last) | passed | the 429 with `Retry-After: 60` came after 61 requests |
| Browser | load, connect-screen | passed | the connect screen renders with no console error; axe reports no violation |
| Browser | connect, workbench | passed | the token is accepted and the workbench renders; axe reports no violation |
| Browser | prompt-and-answer | passed | a new conversation, a typed prompt, a streamed answer; "Response complete." is announced; the transcript shows no "Unsupported content" |
| Browser | second-prompt | passed | a second prompt in the same conversation (the head subscribes again) |
| Browser | credential-storage | passed | the token is in none of `localStorage`, `sessionStorage`, IndexedDB or cookies |
| Browser | no-browser-problems | passed | no console error, no failed request, no page error during the whole run |
| Browser | sign-out | passed | sign-out forgets the token and returns to the connect screen |

Files: `e2e-api.json`, `e2e-api-rate-limit.json`, `browser-report.json`, and the screenshots in `screenshots/` (`connect.png`, `workbench.png`, `answer.png`, `second-answer.png`).

## What the runs found

The first seven runs failed or showed problems that are fixed; the harness and the product both changed because of them.

- The API ends the stream of a session whose run has completed, so the driver posts the prompt first and then reads the stream (`e2e-api.mjs`).
- omp puts a `<system-reminder>` block in front of every prompt on the same line; the scripted model strips it before it looks for its directives.
- The vendored terminal stylesheet was imported from inside another stylesheet; axe-core re-fetched the nested import relative to the page and got three 404s. It is now imported from the entry stylesheet.
- **After every turn of a real omp session the transcript showed a disclosure "Unsupported content (ContextUsageEvent)".** omp reports the context window use at the end of each turn; the API mapped it to an unknown-kind event named after its CLR type and the web head rendered unknown kinds. The event is now stored as `context_usage` with its two numbers and the transcript does not render it (`849780ef`); the browser run fails if "Unsupported content" appears after a plain answer.

## What this does not prove

- A real model. The model is a script; nothing is learned about tool use, quality or cost of a provider (OD-07, OQ-172).
- A deployment. No Railway project, no public origin, no TLS, no forwarded headers, no container image. The Containerfile changes of the security review (read-only `/app`, `DOTNET_EnableDiagnostics=0`) were not built.
- Other tools than `bash`, other browsers, a screen reader, a phone.
- Throughput or load; the rate limit was exercised once.

## How to repeat

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH
dotnet build services/Autonogrammer.Api/Autonogrammer.Api.csproj -c Release --no-restore
AUTONOGRAMMER_API_ORIGIN=http://127.0.0.1:5187 AUTONOGRAMMER_API_ORIGIN_ALLOW_LOOPBACK=1 bash scripts/vercel/build-web.sh /tmp/web-real
bash scripts/hosted/run-local.sh --web-bundle /tmp/web-real --out /tmp/hosted-run
```

The script stops every process it started and removes its work directory. See [`scripts/hosted/README.md`](../../../../scripts/hosted/README.md).
