# Cloud agent worker image under podman, 2026-10-28

**Result: 14 of 14 checks passed.** The worker image built from `deploy/worker.Containerfile` started under podman, refused every bad token, and bridged a real `omp acp` (omp 18.12.5, the version the image pins) to a WebSocket client through `initialize` and `session/new`. No model was called, nothing was deployed to Railway.

## Method

1. `podman build -f deploy/worker.Containerfile -t ompos-agent-worker:local .` (exit 0; base images `mcr.microsoft.com/dotnet/sdk:10.0.402-noble` and `aspnet:10.0.13-noble`, Bun 1.4.3 and omp 18.12.5 downloaded and checked against the pinned digests).
2. `node scripts/cloud/worker-live-check.mjs` generated an ECDSA P-256 key pair, started the image with the public key (`Worker__Auth__PublicKeysPem__0`, issuer `ompos-api`) on `127.0.0.1:18088`, minted ES256 tokens the way the control plane does (`iss`, `aud` `ompos-worker`, `sub` the session id, `iat`, `exp`), ran the checks below and removed the container and its volume.

## Checks

| # | Check | Result |
|---|-------|--------|
| 1 | `GET /healthz` answers 200 `ok` | Pass |
| 2 | `GET /readyz` answers 200 `{"status":"ready"}` | Pass |
| 3 | `GET /acp` without a token | 401 |
| 4 | Token with another audience | 401 |
| 5 | Expired token | 401 |
| 6 | Token living longer than 15 minutes | 401 |
| 7 | Token whose subject is `../etc` | 403 |
| 8 | Valid token without a WebSocket upgrade | 400 |
| 9 | WebSocket upgrade with a valid token | Opened |
| 10 | `initialize` through the bridge | Answered by `omp` 18.12.5 |
| 11 | `loadSession` advertised | `true` |
| 12 | `session/new` with `cwd` under `/data/work/<session id>` | Returned a session id |
| 13 | A second connection for the same session | Refused (409; the log says another connection is attached) |
| 14 | Container log | No key or token material |

## What this proves and what it does not

- Proves: the image builds with the pinned Bun and omp, the entrypoint drops privileges and the program listens on `[::]:$PORT`, the JWT gate rejects the five bad-token cases before any upgrade, the bridge carries ACP frames both ways to a real omp, and one session has one attached connection.
- Does not prove: a prompt turn (needs a model credential, OD-07); a Railway deploy (nothing of OmpOS is deployed; OQ-73 and OQ-100 stay open); a control plane that mints the tokens (the script plays that role); a git checkout, a branch or a pull request from the worker (the worker bootstrap is not built, see the cloud agents reference); hooks in the worker (covered by unit tests with fakes); sleep and wake on Railway.
