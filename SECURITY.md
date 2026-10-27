# Security policy

## Supported versions

Security fixes are made on the latest release line and on `main`.

| Version | Supported |
| --- | --- |
| `main` (development head) | yes |
| 0.15.x | yes |
| 0.14.x and earlier | no (upgrade to 0.15.x) |

OmpOS is pre-1.0: a fix ships in the next 0.15.x patch release, not as a back-port to older lines.

## Reporting a vulnerability

Report privately. Do not open a public issue, discussion or pull request for a vulnerability.

1. Open the repository's **Security** tab and choose **Report a vulnerability**, or go to
   <https://github.com/swcstudiospace/OmpOS/security/advisories/new>.
2. Describe the problem: the affected component and version or commit, the steps to reproduce, the impact you expect, and a suggested fix if you have one.
3. Keep the report private until a fix is released and we have agreed a disclosure date.

GitHub's private vulnerability reporting is the only supported channel. If the **Report a vulnerability** button is missing, the feature has not been enabled yet: open a public issue that says only "private reporting is unavailable" (no details), and a maintainer will enable it and ask you to report there.

### What to expect (targets, not promises)

OmpOS is maintained by a small team without a service-level agreement. These are goals we aim for:

| Step | Target |
| --- | --- |
| Acknowledge the report | within 5 working days |
| First assessment (accepted, needs more information, or not a vulnerability) | within 10 working days |
| Fix or mitigation for a confirmed high or critical issue | within 90 days |
| Credit in the advisory and the changelog | with your agreement |

We publish a GitHub security advisory for every confirmed vulnerability and note the fix under **Security** in [CHANGELOG.md](CHANGELOG.md).

## Scope

In scope (the code in this repository):

- the provider-key store and the handling of API keys in the desktop and web heads (keys must never reach logs, exports, durable browser storage or URLs);
- the hosted API (`services/OmpOS.Api`): authentication, session and run ownership, tool approvals, the cancel and steering paths;
- the sandbox and process boundaries: container sandboxes, the `omp` launch contract, terminal and remote (SSH) environments;
- the plugin host, hooks and MCP gateway: consent, isolation, command execution;
- the web head: content-security-policy, origin checks, stream handling;
- build and release integrity: workflows in `.github/`, scripts under `scripts/`, pinned downloads.

Out of scope:

- vulnerabilities in the sibling plugin repositories (`claude-ultrathink`, `agent-swarm`, `agent-substrate`), the `omp` engine, or third-party packages themselves: report them to their own projects (we do track NuGet and npm advisories through Dependabot);
- findings that need a machine or account the attacker already controls (for example local root, or a user who deliberately disables a safeguard);
- denial of service by volume against a deployment you do not operate;
- social engineering of maintainers, and reports from automated scanners without a demonstrated impact;
- the demo web build, which uses a simulated engine and holds no real data.

## Rules for everyone (including in public issues)

- **Never paste secrets into an issue, discussion or pull request**: API keys, bearer tokens, session cookies, private keys, `.env` contents, `agent.db` or `models.yml` files with keys. Redact them first. If a secret was posted by accident, revoke it at its provider immediately and then ask a maintainer to delete the content.
- Do not test against infrastructure you do not own, and do not access, modify or retain data that is not yours.
- Use only the minimum proof needed to show the impact.

## Safe harbour

We will not pursue action against researchers who follow this policy, act in good faith, avoid privacy violations and service disruption, and give us reasonable time to fix a problem before disclosing it.
