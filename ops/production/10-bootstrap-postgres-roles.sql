-- PostgreSQL production roles bootstrap, revision 10.
--
-- The official postgres image runs this file only while initializing an empty
-- PGDATA directory. It is deliberately mounted read-only by
-- compose.production.yml. Role names and passwords are loaded with psql's
-- \getenv command from the root-owned production.env file; no credential is
-- stored in this repository or passed as a process argument.
--
-- This hook creates the schemas before EF Core migrations. That lets the
-- migrator's default privileges apply to every table and sequence it creates.
\set ON_ERROR_STOP on

\getenv owner_user PRODUCTION_POSTGRES_OWNER_USER
\getenv runtime_user PRODUCTION_POSTGRES_RUNTIME_USER
\getenv runtime_password PRODUCTION_POSTGRES_RUNTIME_PASSWORD
\getenv backup_user PRODUCTION_POSTGRES_BACKUP_USER
\getenv backup_password PRODUCTION_POSTGRES_BACKUP_PASSWORD

-- Fail without echoing any protected value if a caller bypassed Compose's :?
-- required-variable checks.
SELECT CASE
  WHEN :'owner_user' <> ''
   AND :'runtime_user' <> ''
   AND :'runtime_password' <> ''
   AND :'backup_user' <> ''
   AND :'backup_password' <> '' THEN 1
  ELSE 1 / 0
END;

-- The postgres image creates the owner role and database before initdb hooks.
-- Runtime and backup are distinct LOGIN roles without administrative powers.
SELECT format(
  'CREATE ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L',
  :'runtime_user', :'runtime_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'runtime_user')
\gexec

SELECT format(
  'ALTER ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L',
  :'runtime_user', :'runtime_password')
\gexec

SELECT format(
  'CREATE ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L',
  :'backup_user', :'backup_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'backup_user')
\gexec

SELECT format(
  'ALTER ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L',
  :'backup_user', :'backup_password')
\gexec

-- Public must not be able to create objects in the application database.
REVOKE CREATE ON DATABASE hato_production FROM PUBLIC;
REVOKE ALL ON SCHEMA public FROM PUBLIC;

-- The current connection is the initial owner/migrator. Create each schema
-- explicitly so grants can be applied before EF Core creates its first table.
SELECT format('CREATE SCHEMA IF NOT EXISTS %I AUTHORIZATION %I', schema_name, :'owner_user')
FROM unnest(ARRAY['livestock', 'production', 'inventory', 'people', 'breeding', 'tasks']) AS schema_name
\gexec

-- The runtime can only perform DML on application relations; it never gets
-- CREATE on a schema or database. The backup role remains read-only.
SELECT format('GRANT USAGE ON SCHEMA %I TO %I, %I', schema_name, :'runtime_user', :'backup_user')
FROM unnest(ARRAY['livestock', 'production', 'inventory', 'people', 'breeding', 'tasks']) AS schema_name
\gexec

SELECT format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO %I', schema_name, :'runtime_user')
FROM unnest(ARRAY['livestock', 'production', 'inventory', 'people', 'breeding', 'tasks']) AS schema_name
\gexec

SELECT format('GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %I TO %I', schema_name, :'runtime_user')
FROM unnest(ARRAY['livestock', 'production', 'inventory', 'people', 'breeding', 'tasks']) AS schema_name
\gexec

SELECT format('GRANT SELECT ON ALL TABLES IN SCHEMA %I TO %I', schema_name, :'backup_user')
FROM unnest(ARRAY['livestock', 'production', 'inventory', 'people', 'breeding', 'tasks']) AS schema_name
\gexec

SELECT format('GRANT SELECT ON ALL SEQUENCES IN SCHEMA %I TO %I', schema_name, :'backup_user')
FROM unnest(ARRAY['livestock', 'production', 'inventory', 'people', 'breeding', 'tasks']) AS schema_name
\gexec

-- Migrations run as owner/migrator, so these grants also cover future tables
-- and sequences without elevating runtime or backup.
SELECT format(
  'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I',
  :'owner_user', schema_name, :'runtime_user')
FROM unnest(ARRAY['livestock', 'production', 'inventory', 'people', 'breeding', 'tasks']) AS schema_name
\gexec

SELECT format(
  'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT USAGE, SELECT ON SEQUENCES TO %I',
  :'owner_user', schema_name, :'runtime_user')
FROM unnest(ARRAY['livestock', 'production', 'inventory', 'people', 'breeding', 'tasks']) AS schema_name
\gexec

SELECT format(
  'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT SELECT ON TABLES TO %I',
  :'owner_user', schema_name, :'backup_user')
FROM unnest(ARRAY['livestock', 'production', 'inventory', 'people', 'breeding', 'tasks']) AS schema_name
\gexec

SELECT format(
  'ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA %I GRANT SELECT ON SEQUENCES TO %I',
  :'owner_user', schema_name, :'backup_user')
FROM unnest(ARRAY['livestock', 'production', 'inventory', 'people', 'breeding', 'tasks']) AS schema_name
\gexec

-- The postgres image necessarily makes its initial POSTGRES_USER a superuser;
-- this is the owner/migrator and it is never supplied to runtime or backup.
-- PostgreSQL does not let a session remove its own SUPERUSER attribute, so
-- de-escalating that bootstrap identity requires a separately administered
-- database bootstrap role. That operational split is intentionally not hidden
-- here: this file establishes the three service identities and least privilege
-- boundary required by this stack.
