# Development API on the VPS (option A)

Status: running on the development VPS as of 2026-11-03. Private to the operator's tailnet. Nothing is public: no port on the public address, no DNS record, no Railway service.

Scope: one operator, development use. It is not the production hosting of [HOST-01](hosting.md) and it does not replace the Railway definition. Owner-token authentication is an interim mode for this host. The clients move to Supabase sign-in in the sign-in change, and the API then runs in `Oidc` mode against the shared project (see "Switch the API to Oidc" below).

## Topology

| Piece | Where it runs | Published | Role |
|-------|---------------|-----------|------|
| `autonogrammer-postgres` (TimescaleDB 2.13, PostgreSQL 16) | compose stack, profile `backend` | `127.0.0.1:5432` | Session store, schema `autonogrammer` |
| `autonogrammer-dragonfly` (Dragonfly 1.27.1) | compose stack, profile `backend` | `127.0.0.1:6380` (6379 is the host Redis) | Run leases, idempotency, SSE wake-up |
| `autonogrammer-api` (built from `deploy/api.Containerfile`) | `podman run` on network `deploy_default` | `127.0.0.1:8090` to container port 8080 | The API |
| `tailscale serve` mapping | host | `https://<tailnet-host>:10443`, tailnet only | The only way in from other devices |

The API is not a compose service. podman-compose 1.6 builds a service only from the `dockerfile` key, and the repository hygiene rule forbids that word in compose. The API is built and run with podman commands instead, as below.

## Secrets (root-only, outside the repository)

| File | Mode | Contents |
|------|------|----------|
| `/etc/autonogrammer/compose.env` | 600 | Superuser, Dragonfly and role passwords, tenant hash pepper, owner-token SHA-256 |
| `/etc/autonogrammer/api.env` | 600 | Runtime configuration of the API, generated from `compose.env` |
| `/etc/autonogrammer/migrate.env` | 600 | The migrator connection string only (the API never receives it) |
| `/root/.config/autonogrammer/owner-token` | 600 | Optional copy of the raw owner token for `Authorization: Bearer` calls from the operator's shell. Delete it once the token is kept elsewhere: the incident runbook types the token once and never stores it (`docs/runbooks/incident-response.md`) |

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
| Tailnet | `curl https://<tailnet-host>:10443/health/live` | 200 |
| Public address (must fail) | `curl -m 5 http://<public-ip>:8090/health/live` | no connection |
| Migration | step 4 | exit 0, "Applied 10 migration(s); the schema is at head version 10." |
| Secrets in logs | `podman logs autonogrammer-api` searched for the token and the role passwords | 0 matches |

Dragonfly and Postgres publish on loopback only. Dragonfly reports `unhealthy` for a few seconds after start and then `healthy`; the API's readiness is the check that matters, because it authenticates to Dragonfly with the configured password.

## Operate

- Logs: `podman logs --tail 100 autonogrammer-api`.
- Restart after a configuration change: `podman rm -f autonogrammer-api`, then step 5.
- After a host reboot the API does not come back by itself: `--restart on-failure` restarts after a crash, not after a reboot. Run step 1, then step 5 again. No systemd or Quadlet unit exists for this host yet.
- Rotate the owner token: generate a new token, write its digest to `AUTONOGRAMMER_OWNER_TOKEN_SHA256` in `compose.env` and to `Api__Auth__OwnerToken__Sha256__0` in `api.env`, restart the API, then replace or delete the token file.
- Stop exposure without stopping the API: `tailscale serve --https=10443 off`.
- Stop everything: `podman rm -f autonogrammer-api`, then `podman-compose --env-file /etc/autonogrammer/compose.env --profile backend -f deploy/compose.yaml down`. Do not add `-v` for this stack: it removes every volume of the project `deploy`, including `deploy_temporal-data` of the old Temporal stack. To destroy the database, remove its volume by name (`deploy_postgres-data`) after the stack is down.

## Switch the API to Oidc (owner action, not done)

The owner decides when. The switch replaces owner-token mode; it cannot run beside it. In owner-token mode the API refuses any `Api__Auth__Client__*` setting, and `/auth/config` answers `owner-token` whatever the client block says, so set these values together in `/etc/autonogrammer/api.env` (never in the repository):

| Key | Value |
|-----|-------|
| `Api__Auth__Mode` | `Oidc` (replaces `OwnerToken`) |
| `Api__Auth__Authority` | `https://<project-ref>.supabase.co/auth/v1` |
| `Api__Auth__Audience` | `authenticated` |
| `Api__Auth__ValidIssuer` | `https://<project-ref>.supabase.co/auth/v1` |
| `Api__Auth__Algorithms__0` | `ES256` |
| `Api__Auth__AllowedSubjects__0` | the owner's Supabase user id (the `sub` claim) |
| `Api__Auth__Client__ProjectUrl` | `https://<project-ref>.supabase.co` |
| `Api__Auth__Client__PublishableKey` | the publishable key the owner supplies; never a secret key |
| `Api__Auth__Client__Providers__0` (and `__1`) | `google` and/or `x`, only those enabled in the dashboard |
| `Api__Auth__Client__WalletEnabled` | `true` only when Web3 is enabled in the dashboard |

Remove `Api__Auth__OwnerToken__Sha256__0` and `Api__Auth__OwnerToken__Subject`: the owner-token handler is selected only in `OwnerToken` mode, so they do nothing after the switch. Outside Development the validator requires the authority, audience, issuer and at least one allowed subject, or `Api__Auth__AllowAnyAuthenticatedUser=true`, which lets every account the project admits run the agent. The owner's user id is in the project's Authentication users list [INFERENCE: dashboard location not checked].

Then restart (`podman rm -f autonogrammer-api`, then step 5) and check: `GET /auth/config` answers `"mode":"oidc"` with the authority, audience, project URL, publishable key and `methods`, including `providers` and `wallet` as declared, and `GET /sessions` without a token answers 401. The accounts runbook is `docs/runbooks/accounts.md`.

## Known limits and open items

- **Kerberos probe at start.** Npgsql logs `Cannot load library libgssapi_krb5.so.2` once per start, because the noble runtime image lacks the optional GSSAPI library. It is not a failure: the migration applies, readiness answers 200 and sessions work. The fix is to install `libgssapi-krb5-2` in the runtime stage of `deploy/api.Containerfile`; it is not applied yet.
- **No TLS inside the stack.** The database connection uses `SSL Mode=Disable` on the private podman network, and Postgres publishes only on loopback. This is acceptable for this host only; the hosted deployment keeps TLS (`deploy/railway/README.md`).
- **Loopback trust inside the database container.** The image's `pg_hba.conf` trusts the local socket and 127.0.0.1, so a process that can `exec` into `autonogrammer-postgres` reaches the database without a password. Connections from the host and from other containers use scram-sha-256. Checked 2026-11-03 from a sibling container on `deploy_default` with `psql -h postgres`: the `AUTONOGRAMMER_POSTGRES_PASSWORD` value in `compose.env` returns `1`, and `validation-only` fails with exit 2. An `exec` check proves nothing about the password, because `exec` uses the trusted socket.
- **No model turn can start.** No provider credential is set, and the approval list is empty, so every engine tool request is denied. The engine launches only when a session is started.
- **Workspace is in memory.** `Harness__Acp__WorkspaceRoot` is a tmpfs under `/tmp`; agent workspaces do not survive a restart.
- **Owner-token mode is interim.** The web head no longer offers an owner-token sign-in: pointed at this API it shows the sign-in explanation. The desktop head still reads `AUTONOGRAMMER_API_TOKEN` when signed out. Both move to Supabase sign-in with the switch below.
