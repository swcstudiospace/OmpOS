# Development API on the VPS (option A)

Status: running on the development VPS as of 2026-11-03. Private to the operator's tailnet. Nothing is public: no port on the public address, no DNS record, no Railway service.

Scope: one operator, development use. It is not the production hosting of [HOST-01](hosting.md) and it does not replace the Railway definition. Owner-token authentication is an interim mode for this host. The clients move to Supabase sign-in in the sign-in change, and the API then runs in `Oidc` mode against the shared project.

## Topology

| Piece | Where it runs | Published | Role |
|-------|---------------|-----------|------|
| `autonogrammer-postgres` (TimescaleDB 2.13, PostgreSQL 16) | compose stack, profile `backend` | `127.0.0.1:5432` | Session store, schema `autonogrammer` |
| `autonogrammer-dragonfly` (Dragonfly 1.27.1) | compose stack, profile `backend` | `127.0.0.1:6380` (6379 is the host Redis) | Run leases, idempotency, SSE wake-up |
| `autonogrammer-api` (built from `deploy/api.Containerfile`) | `podman run` on network `deploy_default` | `127.0.0.1:8090` to container port 8080 | The API |
| `tailscale serve` mapping | host | `https://vps.hedgehog-mooneye.ts.net:10443`, tailnet only | The only way in from other devices |

The API is not a compose service. podman-compose 1.6 builds a service only from the `dockerfile` key, and the repository hygiene rule forbids that word in compose. The API is built and run with podman commands instead, as below.

## Secrets (root-only, outside the repository)

| File | Mode | Contents |
|------|------|----------|
| `/etc/autonogrammer/compose.env` | 600 | Superuser, Dragonfly and role passwords, tenant hash pepper, owner-token SHA-256 |
| `/etc/autonogrammer/api.env` | 600 | Runtime configuration of the API, generated from `compose.env` |
| `/etc/autonogrammer/migrate.env` | 600 | The migrator connection string only (the API never receives it) |
| `/root/.config/autonogrammer/owner-token` | 600 | The raw owner token, for `Authorization: Bearer` calls from the operator |

Generate the token as the runbook for sign-in incidents does (`docs/runbooks/incident-response.md`): URL-safe base64 of 48 random bytes. The configured digest is the SHA-256 of its UTF-8 bytes, in lowercase hexadecimal. Never export these variables into a shell that outlives the command: an exported value overrides `--env-file` in podman-compose, and a stack started that way keeps the wrong password. Source `compose.env` inside a subshell only.

## Start

Run from the repository root at a clean committed revision.

1. Start the database and Dragonfly:

   ```sh
   podman-compose --env-file /etc/autonogrammer/compose.env --profile backend -f deploy/compose.yaml up -d postgres dragonfly
   ```

2. Create the least-privilege roles (idempotent; a second run rotates the passwords). The script is run as the cluster administrator inside the container, with the role secrets passed through the exec environment:

   ```sh
   ( set -a; . /etc/autonogrammer/compose.env; set +a
     export AUTONOGRAMMER_MIGRATOR_PASSWORD="$AUTONOGRAMMER_MIGRATOR_DB_PASSWORD" AUTONOGRAMMER_APP_PASSWORD="$AUTONOGRAMMER_APP_DB_PASSWORD"
     podman exec -i -e AUTONOGRAMMER_MIGRATOR_PASSWORD -e AUTONOGRAMMER_APP_PASSWORD autonogrammer-postgres \
       psql -X -v ON_ERROR_STOP=1 -U autonogrammer -d postgres < deploy/data-plane/bootstrap.sql )
   ```

3. Build the API image from the committed revision:

   ```sh
   podman build -f deploy/api.Containerfile --build-arg AUTONOGRAMMER_REVISION="$(git rev-parse HEAD)" --build-arg AUTONOGRAMMER_VERSION=0.15.0 -t autonogrammer-api:local .
   ```

4. Apply the schema under the migrator role (exit 0 and the head version are the success signal):

   ```sh
   podman run --rm --network deploy_default --env-file /etc/autonogrammer/migrate.env autonogrammer-api:local migrate
   ```

5. Run the API, published on loopback only:

   ```sh
   podman run -d --name autonogrammer-api --restart on-failure --network deploy_default \
     --publish 127.0.0.1:8090:8080 --tmpfs /tmp --env-file /etc/autonogrammer/api.env autonogrammer-api:local
   ```

6. Expose it on the tailnet only:

   ```sh
   tailscale serve --bg --https=10443 http://127.0.0.1:8090
   ```

## Verify

| Check | Command | Observed 2026-11-03 |
|-------|---------|---------------------|
| Liveness | `curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:8090/health/live` | 200 |
| Readiness (database and Dragonfly with authentication) | `curl ... /health/ready` | 200 |
| No credential | `curl ... /sessions` | 401 |
| Owner token | `curl -H "Authorization: Bearer $(cat /root/.config/autonogrammer/owner-token)" .../sessions` | 200 |
| Wrong token | same with another value | 401 |
| Tailnet | `curl https://vps.hedgehog-mooneye.ts.net:10443/health/live` | 200 |
| Public address (must fail) | `curl -m 5 http://187.77.130.10:8090/health/live` | no connection |
| Migration | step 4 | exit 0, "Applied 10 migration(s); the schema is at head version 10." |
| Secrets in logs | `podman logs autonogrammer-api` searched for the token and the role passwords | 0 matches |

Dragonfly and Postgres publish on loopback only. Dragonfly reports `unhealthy` for a few seconds after start and then `healthy`; the API's readiness is the check that matters, because it authenticates to Dragonfly with the configured password.

## Operate

- Logs: `podman logs --tail 100 autonogrammer-api`.
- Restart after a configuration change: `podman rm -f autonogrammer-api`, then step 5.
- Rotate the owner token: generate a new token, write its digest to `OWNER_TOKEN_SHA256` in `compose.env` and to `Api__Auth__OwnerToken__Sha256__0` in `api.env`, restart the API, then replace the token file.
- Stop exposure without stopping the API: `tailscale serve --https=10443 off`.
- Stop everything: `podman rm -f autonogrammer-api`, then `podman-compose --env-file /etc/autonogrammer/compose.env --profile backend -f deploy/compose.yaml down`. Add `-v` only to destroy the database volumes.

## Known limits and open items

- **Kerberos probe at start.** Npgsql logs `Cannot load library libgssapi_krb5.so.2` once per start, because the noble runtime image lacks the optional GSSAPI library. It is not a failure: the migration applies, readiness answers 200 and sessions work. The fix is to install `libgssapi-krb5-2` in the runtime stage of `deploy/api.Containerfile`; it is not applied yet.
- **No TLS inside the stack.** The database connection uses `SSL Mode=Disable` on the private podman network, and Postgres publishes only on loopback. This is acceptable for this host only; the hosted deployment keeps TLS (`deploy/railway/README.md`).
- **Loopback trust inside the database container.** The image's `pg_hba.conf` trusts the local socket and 127.0.0.1, so a process that can `exec` into `autonogrammer-postgres` reaches the database without a password. Connections from the host and from other containers use scram-sha-256: a login from another container on the stack network fails with the wrong password and succeeds with the one in `compose.env`.
- **No model turn can start.** No provider credential is set, and the approval list is empty, so every engine tool request is denied. The engine launches only when a session is started.
- **Workspace is in memory.** `Harness__Acp__WorkspaceRoot` is a tmpfs under `/tmp`; agent workspaces do not survive a restart.
- **Owner-token mode is interim.** The web and desktop clients still hold a token in the Connect flow until the sign-in change lands.
