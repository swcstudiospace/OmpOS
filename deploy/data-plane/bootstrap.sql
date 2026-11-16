-- Operator-run bootstrap of the Autonogrammer PostgreSQL data plane (ADR-0031 D4, decisions D-14 and D-15).
--
-- It creates, idempotently and only through the cluster administrator role (a superuser, or on a managed server the role that may create roles,
-- databases and the extension): the roles, the database, the TimescaleDB extension in `public`, the schema, the schema grants and the default
-- privileges. The application never creates any of them. The migration runner (`Autonogrammer.Api migrate`, run as autonogrammer_migrator)
-- creates the tables in the schema afterwards.
--
-- Roles (cluster-global, so the autonogrammer_ prefix keeps them apart from every other tenant of the server):
--   autonogrammer_owner     NOLOGIN. Owns the database and the schema; the migrator is a member.
--   autonogrammer_app       LOGIN. The runtime role: data manipulation only, no DDL; INSERT and SELECT only on the append-only dispatch_audit table.
--   autonogrammer_ro        LOGIN, optional. Read only; created only when its secret is given.
-- SOTA: roles fail-closed (app has no DDL per AG-68-5); bootstrap is operator only, no deploy mutation.
--
-- Secrets never come from this file. Give each role's secret as a psql variable (migrator_password, app_password, ro_password) or as an
-- environment variable (AUTONOGRAMMER_MIGRATOR_PASSWORD, AUTONOGRAMMER_APP_PASSWORD, AUTONOGRAMMER_RO_PASSWORD); a variable wins over the
-- environment. A missing mandatory secret stops the script with an error before any role is created. Run it again to rotate the secrets: every
-- statement is idempotent. The database name defaults to `autonogrammer`; the psql variable `db` overrides it (the role test uses a throwaway name).
-- Note for the operator: an ALTER ROLE statement that carries a secret is written to the server log when log_statement is `ddl` or `all`; keep it at `none`.
--
-- Usage (administrative connection to any existing database, normally `postgres`):
--   psql -X -v ON_ERROR_STOP=1 -f deploy/data-plane/bootstrap.sql

\set ON_ERROR_STOP on

\if :{?db}
\else
  \set db autonogrammer
\endif

\if :{?migrator_password}
\else
  \getenv migrator_password AUTONOGRAMMER_MIGRATOR_PASSWORD
\endif
\if :{?app_password}
\else
  \getenv app_password AUTONOGRAMMER_APP_PASSWORD
\endif
\if :{?ro_password}
\else
  \getenv ro_password AUTONOGRAMMER_RO_PASSWORD
\endif

\if :{?migrator_password}
\else
  \echo 'error: the migrator secret is missing: set the psql variable migrator_password or the environment variable AUTONOGRAMMER_MIGRATOR_PASSWORD'
  DO $$ BEGIN RAISE EXCEPTION 'autonogrammer bootstrap: the migrator secret is missing'; END $$;
\endif
\if :{?app_password}
\else
  \echo 'error: the runtime secret is missing: set the psql variable app_password or the environment variable AUTONOGRAMMER_APP_PASSWORD'
  DO $$ BEGIN RAISE EXCEPTION 'autonogrammer bootstrap: the runtime secret is missing'; END $$;
\endif

-- Roles. The creation is guarded, and the secrets are set on every run, so a second run rotates them.
SELECT format('CREATE ROLE %I NOLOGIN', 'autonogrammer_owner')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'autonogrammer_owner')
\gexec

SELECT format('CREATE ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE', 'autonogrammer_migrator')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'autonogrammer_migrator')
\gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L', 'autonogrammer_migrator', :'migrator_password')
\gexec

SELECT format('CREATE ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE', 'autonogrammer_app')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'autonogrammer_app')
\gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L', 'autonogrammer_app', :'app_password')
\gexec

\if :{?ro_password}
SELECT format('CREATE ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE', 'autonogrammer_ro')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'autonogrammer_ro')
\gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L', 'autonogrammer_ro', :'ro_password')
\gexec
\endif

GRANT autonogrammer_owner TO autonogrammer_migrator;
GRANT autonogrammer_owner TO CURRENT_USER;

-- The database. Nobody but the roles named here may connect to it.
SELECT format('CREATE DATABASE %I OWNER %I', :'db', 'autonogrammer_owner')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'db')
\gexec

REVOKE CONNECT ON DATABASE :"db" FROM PUBLIC;

SELECT format('GRANT CONNECT ON DATABASE %I TO %I', :'db', rolname)
FROM pg_roles
WHERE rolname IN ('autonogrammer_migrator', 'autonogrammer_app', 'autonogrammer_ro')
\gexec

\connect :"db"

-- The extension lives in `public`, once, here and never in a migration step. Nothing else is created in `public`.
CREATE EXTENSION IF NOT EXISTS timescaledb WITH SCHEMA public;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

-- The schema belongs to the owner role, and so do any tables a deployment of an earlier version created under another role.
CREATE SCHEMA IF NOT EXISTS autonogrammer AUTHORIZATION autonogrammer_owner;
ALTER SCHEMA autonogrammer OWNER TO autonogrammer_owner;

SELECT format('ALTER TABLE %I.%I OWNER TO %I', schemaname, tablename, 'autonogrammer_migrator')
FROM pg_tables
WHERE schemaname = 'autonogrammer' AND tableowner <> 'autonogrammer_migrator'
\gexec

GRANT USAGE, CREATE ON SCHEMA autonogrammer TO autonogrammer_migrator;
GRANT USAGE ON SCHEMA autonogrammer TO autonogrammer_app;

-- Objects the migrator (or the owner) creates later are readable and writable by the runtime role without a grant in each migration step.
-- The append-only audit table is the exception: step M0005 revokes UPDATE and DELETE on it when this role exists.
ALTER DEFAULT PRIVILEGES FOR ROLE autonogrammer_migrator IN SCHEMA autonogrammer
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO autonogrammer_app;
ALTER DEFAULT PRIVILEGES FOR ROLE autonogrammer_migrator IN SCHEMA autonogrammer
  GRANT USAGE, SELECT ON SEQUENCES TO autonogrammer_app;
ALTER DEFAULT PRIVILEGES FOR ROLE autonogrammer_owner IN SCHEMA autonogrammer
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO autonogrammer_app;
ALTER DEFAULT PRIVILEGES FOR ROLE autonogrammer_owner IN SCHEMA autonogrammer
  GRANT USAGE, SELECT ON SEQUENCES TO autonogrammer_app;

-- Tables that already exist (a re-run after the migrations, or an upgrade) get the same grants now.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA autonogrammer TO autonogrammer_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA autonogrammer TO autonogrammer_app;

SELECT format('REVOKE UPDATE, DELETE, TRUNCATE ON %s FROM autonogrammer_app', to_regclass('autonogrammer.dispatch_audit'))
WHERE to_regclass('autonogrammer.dispatch_audit') IS NOT NULL
\gexec

\if :{?ro_password}
GRANT USAGE ON SCHEMA autonogrammer TO autonogrammer_ro;
ALTER DEFAULT PRIVILEGES FOR ROLE autonogrammer_migrator IN SCHEMA autonogrammer GRANT SELECT ON TABLES TO autonogrammer_ro;
ALTER DEFAULT PRIVILEGES FOR ROLE autonogrammer_owner IN SCHEMA autonogrammer GRANT SELECT ON TABLES TO autonogrammer_ro;
GRANT SELECT ON ALL TABLES IN SCHEMA autonogrammer TO autonogrammer_ro;
\endif
