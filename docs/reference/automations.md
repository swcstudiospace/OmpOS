---
description: "Reference for OmpOS automations: cron and signed webhook triggers, cloud and desktop targets, limits, the API routes and the Automations settings page."
---

# Automations

Status: Partial (library, API routes, receiver, scheduler and desktop Settings page are built, wired and tested; the API is off by default; cron only on the desktop; no web page; no live cloud worker, GitHub repository or model turn)

Last verified: 2026-10-28

Applies to: v0.15.0

An automation is a prompt that OmpOS runs for you when something happens: a cron schedule comes due, or a signed webhook arrives (a generic one, or one from GitHub). Each run starts a cloud agent in your repositories with your prompt and a short description of what triggered it. This is the OmpOS counterpart of Cursor's Automations, limited to the triggers that need no third-party hosting.

> **Status of this release** (recorded 2026-10-28, tree 7535859).
>
> - **Built, wired, proven by tests.** *API:* `AddAutomationServices` and `MapAutomationEndpoints` are called in `services/OmpOS.Api/Program.cs`; `Automations:Enabled` is false by default and every automation route then answers 404 `automations_disabled`; the routes are in `docs/api/openapi.json` (contract version 1.5.0); proof: `tests/OmpOS.Api.IntegrationTests/Automations` (`AutomationManagementTests`, `AutomationRunEndpointTests`, `AutomationWebhookEndpointTests`, `AutomationCloudTargetTests`, `AutomationOptionsValidatorTests`, `HostedAutomationsSettingsProviderTests`; 73 tests, 1027 of 1027 in the whole project on the final tree). *Desktop:* Settings > Automations is the 22nd page of the Settings window; the head runs the scheduler once it is open; proof: `AutomationsHostTests`, `LocalSessionAutomationTargetTests`, `LocalAutomationsManagerTests`, `AutomationsPageViewTests`, `HeadServicesWiringTests` (the scheduler starts at head open).
> - **Built, not wired or limited.** The desktop runs cron only: it refuses webhook triggers and secret rotation (the editor says so). There is no web page: the web head has no automations screen, only the API routes. The real UI path of a local run (`HeadAutomationSession(s)`: the approval card in a live session, the first turn, stopping on timeout) has no test, only the scripted seam is tested. A cloud target started from the desktop applies the first repository's branch and base branch to every repository.
> - **Not live-verified.** No automation ran against a live cloud worker, a live GitHub repository or a live omp session, and no model turn ran (open question OD-07). Nothing of OmpOS is deployed on Railway. Nothing ran on Windows, macOS or Android.

## Turn it on

Automations live in the OmpOS API. They are off by default: with `Automations:Enabled` false every automation route answers 404 `automations_disabled` (including the webhook receiver), nothing is scheduled, and the settings summary `GET /automations` keeps saying that no engine is configured.

| Key | Default | Meaning |
| --- | --- | --- |
| `Automations:Enabled` | `false` | Turns the routes, the scheduler and the receiver on. |
| `Automations:StorePath` | empty | Absolute path of the automation file. Empty keeps automations in memory; a restart loses them. |
| `Automations:SecretStorePath` | empty | Absolute path of the webhook secret file (mode 0600, **not encrypted at rest**). Empty keeps secrets in memory; after a restart every webhook needs a rotated secret. Register your own `IAutomationSecretStore` to use a vault. |
| `Automations:SchedulerEnabled` | `true` | Whether this process starts cron runs. Set it false on every replica but one. |
| `Automations:WebhookRequestsPerMinute` | `120` | One global bucket for the public receiver, 1 to 100000. |
| `Automations:RequestsPerMinute` | `120` | One global bucket for the management routes, 1 to 100000. |
| `Automations:WebhookReplayWindowSeconds` | `300` | How far a generic delivery's timestamp may differ from the server clock, 30 to 3600. |

Cloud targets also need `Cloud:Enabled` (see [cloud agents](../runbooks/cloud-agents.md)): without it every cloud run fails with a sentence that says so.

**Run one scheduler.** The scheduler keeps its cursor in the store. Two API processes that both schedule over one store would each start a cron run. Give one process `SchedulerEnabled: true` and the others `false`. Webhook deliveries are safe on any replica that shares the store and the secret file.

## What an automation holds

| Field | Rule |
| --- | --- |
| `name` | 1 to 100 characters, no control characters. |
| `prompt` | 1 to 20000 characters. The instructions every run starts with. |
| `enabled` | Whether triggers start runs. A new automation is created off unless the request says otherwise. A run by hand works either way. |
| `triggers` | 1 to 8. The automation runs when any trigger fires. |
| `target` | `cloud` (default): 1 to 16 repositories (`https` URL, no credentials, query or fragment; optional `branch` and `baseBranch`), optional `secretRefs` (names of cloud secrets, 32 at most), `environmentId`, `model`, `mode`. `local`: a desktop session, see [Targets](#targets). |
| `allowedTools` | Names from `pull_request_create` (the default), `pull_request_comment`, `pull_request_request_reviewers`. See [Tools](#tools). |
| `maxConcurrentRuns` | 1 to 4, default 1. |
| `maxRunsPerHour` | 1 to 60, default 12. |
| `maxRuntimeMinutes` | 1 to 360, default 30. |
| `timeZone` | An IANA name such as `Europe/Berlin`, or `UTC` (default). Cron triggers are read in this zone. |

An owner may have 50 automations. Secrets are references only: a cloud target names its secrets, and the webhook signing secret lives in the secret store, never in the automation file, a list, a response or a log line.

## Cron triggers

Five fields: `minute hour day-of-month month day-of-week`. There are no seconds.

| Field | Values |
| --- | --- |
| minute | 0 to 59 |
| hour | 0 to 23 |
| day of month | 1 to 31 |
| month | 1 to 12 or `jan` to `dec` |
| day of week | 0 to 7 (0 and 7 are Sunday) or `sun` to `sat` |

Each field takes `*`, a value, a range (`9-17`), a list (`1,15`), and a step (`*/15`, `0-30/10`, `5/20`). Names are case-insensitive. `?`, `L`, `W` and `#` are not supported. The presets `@yearly`, `@annually`, `@monthly`, `@weekly`, `@daily`, `@midnight` and `@hourly` work.

**Day rule.** When both day-of-month and day-of-week are restricted (neither starts with `*`), a day matches if either matches (`0 0 13 * fri` is every Friday and every 13th). Otherwise both must match.

**Never early, maybe late.** A run never starts before its occurrence. The scheduler looks every 15 seconds. If an occurrence was missed (the host was down), it runs once when it is at most 5 minutes late; several missed occurrences fold into that one run. A later one is skipped and recorded as a `skipped` run: "more than 5 minutes late". A disabled automation does not catch up when you enable it.

**Clock changes.** Cron fields are read as the zone's wall clock:

- A local time that does not exist (clocks go forward) fires once, at the instant the clock changes. `30 2 * * *` in New York on the spring change day runs at 03:00. If the hour field is a wildcard (`*` or `*/n`) the skipped times do not fire, as in classic cron.
- A local time that happens twice (clocks go back) fires once, at its first occurrence. If the hour field is a wildcard it fires at both, so `30 * * * *` keeps its half-hour rhythm.
- `29 February` fires only in leap years; an expression that can never match (`0 0 30 2 *`) never fires and the preview says so.

The editor and `GET /automations/cron-preview?expression=…&timeZone=…&count=5` show the next occurrences before you save.

## Webhook triggers

`POST /hooks/automations/{id}` is the receiver. It is anonymous (no login) and authenticated by an HMAC-SHA256 signature over the request, with the automation's own secret. The secret is generated when the automation first gets a webhook trigger and **shown once**, in the answer of the create (or replace) call. Rotate it with `POST /automations/{id}/webhook-secret`; the old one stops working at once. The URL is `{API origin}/hooks/automations/{id}`; the identifier is 128 random bits.

Signatures are `sha256=` followed by 64 lower-case hex digits, compared in constant time. A body is at most **256 KiB**; larger bodies answer 413 before anything else is looked at. An automation has at most one webhook trigger per flavour, and both flavours share one secret. The flavour that applies is the one whose signature header is present.

### Generic flavour

| Header | Value |
| --- | --- |
| `X-OmpOS-Timestamp` | Whole Unix seconds. Must be within the replay window (default ±5 minutes) of the server clock. |
| `X-OmpOS-Signature` | `sha256=` + HMAC-SHA256 of `<timestamp>.<raw body>` with the secret. |
| `X-OmpOS-Delivery` | Optional identifier, shown in the run summary. |

```sh
body='{"deploy":"v1.2"}'
ts=$(date +%s)
sig=$(printf '%s.%s' "$ts" "$body" | openssl dgst -sha256 -hmac "$OMPOS_WEBHOOK_SECRET" -hex | sed 's/^.* //')
curl -X POST "$OMPOS_API/hooks/automations/$ID" \
  -H "X-OmpOS-Timestamp: $ts" -H "X-OmpOS-Signature: sha256=$sig" \
  -H 'Content-Type: application/json' --data "$body"
```

The first 4000 characters of the body go to the run as trigger data, fenced as untrusted (see [Safety](#safety)). A repeat of the same signature inside twice the replay window answers 409 and starts nothing.

### GitHub flavour

In the repository (or organisation) settings add a webhook: payload URL `{API origin}/hooks/automations/{id}`, content type `application/json`, the secret from OmpOS, and the events **Pushes** and **Pull requests**. GitHub sends `X-Hub-Signature-256` (HMAC-SHA256 of the raw body), `X-GitHub-Event` and `X-GitHub-Delivery`.

| Filter | Meaning |
| --- | --- |
| `events` | `push`, `pull_request`. Empty accepts both. Any other event is ignored. `ping` is answered with `pong`. |
| `actions` | Pull request actions, for example `opened`, `synchronize`, `reopened`, `ready_for_review`, and the pseudo action `merged` (closed by a merge). Empty accepts every action. |
| `branches` | Globs on the pushed branch, or on the pull request's **base** branch. `*` matches inside one path segment, `**` across segments, `?` one character. Empty accepts every branch. |

Pushes to tags and branch deletions are ignored. A pull request from a fork is ignored unless it was merged: its text is written by a stranger. A delivery with an identifier seen in the last hour answers 409 (GitHub's "Redeliver" button reuses the identifier). The run summary reads like `push to main by octocat (3 commits)`.

### What the receiver answers

| Answer | When |
| --- | --- |
| 202 `accepted` + `runId` | Authentic, matched a trigger, a run started. |
| 200 `ignored` | Authentic, but the automation is off, or no trigger wants the event, branch, action or payload. |
| 200 `pong` | An authentic GitHub `ping`. |
| 401 `webhook_unauthorized` | **Every** authentication failure: unknown or deleted automation, no webhook trigger, no secret, missing or malformed headers, wrong flavour, wrong signature, stale or malformed timestamp. The body is identical for all of them, so a caller learns nothing about which check failed; the server log has a reason code. |
| 409 `webhook_replayed` | An authentic delivery that repeats an earlier one. |
| 413 `webhook_too_large` | Body over 256 KiB, before authentication. |
| 429 `automation_limit` | Authentic, but the concurrency limit or the hourly cap refused the run. |
| 429 (rate limiter) | More than `WebhookRequestsPerMinute` deliveries from everyone together. `Retry-After: 60`. |
| 404 | Automations are off. |

## Targets

**Cloud** (the API's target). The run creates a cloud agent for the automation's owner through the same service the `/cloud/agents` routes use, so owner caps, secret resolution and repository checks apply, waits until its worker is ready, sends the prompt, and ends as `launched` with the agent's identifier. It does not wait for the agent to finish: follow it at `GET /cloud/agents/{id}`. The run's identifier is the idempotency key, so a retry never creates a second agent. Each run creates an agent, which counts against `Cloud:Limits:MaxAgentsPerOwner` and `MaxConcurrentRunningPerOwner`: when a limit is reached the run fails with the cloud code (for example `agent_limit_reached`). Delete old agents from the Cloud agents page.

**Local.** The API has no local target and refuses `local` targets at save time. The desktop registers one: the Automations section of Settings keeps the automations in `automations.json` in the per-user OmpOS folder, and the head runs the scheduler while the app is open (it starts once the head is open and stops when the app closes). A run starts a new agent session in the open workspace, listed in the rail but not selected; the run mode of the session applies. The first run of each automation (and the first run after its prompt changes) asks you to confirm through an approval card in that session; the answer is kept in `automation-consent.json`. One run per automation at a time and the hourly cap come from the runner; the run ends when the first turn ends and the history records the session identifier. Without omp installed local automations are disabled and the page says so. On the desktop the only trigger is the cron schedule: a webhook needs the API, which has a public address, so the desktop editor shows no webhook trigger and refuses one. A desktop automation with a `cloud` target launches the agent through the cloud agents service when `OMPOS_API_BASE_URL` is set (the branches of the first repository apply to every repository).

## Tools

`allowedTools` carries the names of the extra tools a run may use beyond reading and editing files: `pull_request_create` (on by default), `pull_request_comment`, `pull_request_request_reviewers`. The run states the list in its prompt and the target enforces it; the library does not run a tool itself. For cloud agents, opening a pull request still goes through the cloud agent's pull request service and its own checks.

## Limits and history

- **Concurrency:** at most `maxConcurrentRuns` runs of one automation at once (default 1). A start beyond that is not queued; it is recorded as `skipped`.
- **Hourly cap:** at most `maxRunsPerHour` runs may start in a rolling hour. Skipped attempts do not count; failed runs do.
- **Maximum runtime:** a target that runs to completion is cancelled after `maxRuntimeMinutes` and the run ends `timed_out`.
- **History:** the last 100 runs per automation, newest first (`GET /automations/{id}/runs`): trigger (`manual`, `cron`, `webhook`), summary, status (`running`, `succeeded`, `launched`, `failed`, `timed_out`, `cancelled`, `skipped`), start, duration, error (500 characters at most; a failure's exception message is never copied, because it could hold a secret) and the created agent. A run a previous process left `running` is marked `failed` ("interrupted") at start.

## API

All routes need a login except the receiver. A route answers 404 for an automation of another owner, the same as for one that does not exist.

| Route | Does |
| --- | --- |
| `POST /automations` | Create (201). Body: [`SaveAutomationRequest`](https://github.com/swcstudiospace/OmpOS/blob/main/src/OmpOS.Contracts/Automations/SaveAutomationRequest.cs). Answer carries `webhookSecret` once. |
| `GET /automations/definitions` | The caller's automations with next run and last run. (`GET /automations` is the older settings summary and now lists the same automations in its own shape.) |
| `GET /automations/{id}` | One automation. Never the secret; `hasWebhookSecret` says whether one is stored. |
| `PUT /automations/{id}` | Replace the definition. The secret is kept; one is generated when the first webhook trigger is added and deleted when the last is removed. |
| `PUT /automations/{id}/enabled` | `{ "enabled": true }`. |
| `DELETE /automations/{id}` | Delete with history and secret (204). |
| `POST /automations/{id}/runs` | Run now (202), enabled or not. 409 `automation_busy` or `automation_hourly_cap` when a guard refuses. |
| `GET /automations/{id}/runs` | History. |
| `POST /automations/{id}/webhook-secret` | Rotate; the new secret is shown once. |
| `GET /automations/cron-preview` | Next occurrences of an expression (`expression`, `timeZone`, `count` up to 20). |
| `POST /hooks/automations/{id}` | The receiver. |

Validation failures are 400 `invalid_automation`; `detail` has one `field: message` line per problem (`name`, `prompt`, `triggers`, `cron`, `webhook`, `target`, `repositories`, `secrets`, `tools`, `timeZone`, `limits`). Messages never repeat what was sent.

## Safety

- **Trigger data is untrusted.** Webhook bodies and GitHub event text (titles, commit messages, branch names) come from outside. They reach the run only between `BEGIN UNTRUSTED TRIGGER DATA` and `END UNTRUSTED TRIGGER DATA`, after a sentence that says to treat them as information, never as instructions; control characters are stripped, lengths are bounded and the markers are removed from the data. This reduces prompt injection; it does not remove it. Give an automation only the tools it needs, and prefer branch filters on automations that can open pull requests.
- **Secrets.** The webhook secret appears in exactly one place after creation: the answer to the call that generated it. It is not in the automation file, any list or get answer, a run entry, a prompt, an error, or a log line (tests assert this with canaries). The secret file is owner-only but unencrypted.
- **The receiver is a public endpoint.** It is rate limited globally, size capped, and answers every authentication failure alike. It does not reveal whether an identifier exists.

## Cursor parity

| Cursor row | OmpOS |
| --- | --- |
| AU-01 Automations (prompt, tools, repository scope) | **Built** for cloud targets: prompt, triggers, allowed tools, 1 to 16 repositories. No "no repository" mode yet; a template gallery and the `/automate` skill are not built. |
| AU-02 Scheduled trigger | **Built.** Cron expression, time zone, never early. Presets are the `@` names; the editor has no preset picker yet. |
| AU-03 Source-control triggers | **Partial.** GitHub push and pull request (opened, synchronize, reopened, ready for review, merged) on the webhook receiver. Not built: GitLab, Bitbucket, comments, labels, CI and review events. Fork pull requests are ignored except merged, as in Cursor. |
| AU-04 Slack triggers | Omitted (needs a hosted Slack app). |
| AU-05 Webhook trigger | **Built.** A private signed endpoint per automation. |
| AU-06 Linear, AU-07 Sentry, AU-08 PagerDuty | Omitted (hosted integrations). A webhook trigger can receive their webhooks if the sender can sign with HMAC-SHA256. |
| AU-09 Automation tools | **Partial.** Pull request create, comment and request reviewers by name, enforced by the target. Not built: Send to Slack, Read Slack channels, MCP servers per automation, Memories. |
| AU-10 Run as / Share | Omitted. An automation runs as its owner and is private to them. |
| AU-11 to AU-16 Bugbot, its config, rules, autofix, API and pricing | Omitted: Cursor-hosted. |
| AU-17 Security agents, AU-18 PR routing and approval, AU-19 Rollouts | Omitted: Cursor-hosted. |
