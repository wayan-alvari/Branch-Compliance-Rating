# Database setup and optional MySQL smoke test

MySQL Community Server 8.0.46 is the deployment database. The normal automated
suite uses separate SQLite in-memory databases and requires no MySQL credentials.
The development SQLite demo is explicit and is never selected in production.

## Create a schema and separate users

Run reviewed SQL as a database administrator. Replace every angle-bracket
placeholder locally. Never save the completed SQL or connection strings in Git.

```sql
CREATE DATABASE portfolio_branch_compliance
  CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER '<app_user>'@'localhost' IDENTIFIED BY '<app_password>';
GRANT SELECT, INSERT, UPDATE, DELETE
  ON portfolio_branch_compliance.* TO '<app_user>'@'localhost';

CREATE USER '<migration_user>'@'localhost' IDENTIFIED BY '<migration_password>';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, INDEX, REFERENCES
  ON portfolio_branch_compliance.* TO '<migration_user>'@'localhost';
```

The application identity needs no schema modification or user administration
privileges. The separate migration identity is used only for reviewed migrations.
Use MySQL's matching host grant for the actual local connection transport.

## Configure and apply migrations

The design-time factory generates MySQL migrations without contacting a server.
For migration execution it reads `ConnectionStrings__DefaultConnection` from
the current environment. The runtime also accepts .NET user secrets:

```powershell
dotnet user-secrets set --project src/BranchCompliance.Web `
  "ConnectionStrings:DefaultConnection" `
  "Server=127.0.0.1;Port=3306;Database=portfolio_branch_compliance;User=<app_user>;Password=<app_password>;CharSet=utf8mb4"
```

Set the migration connection in your local environment, then run from the
repository root (dot-source `scripts/Use-LocalSdk.ps1` if using the local SDK):

```powershell
dotnet tool restore
dotnet ef migrations script --idempotent `
  --project src/BranchCompliance.Infrastructure `
  --startup-project src/BranchCompliance.Web
dotnet ef database update `
  --project src/BranchCompliance.Infrastructure `
  --startup-project src/BranchCompliance.Web
```

Review the generated SQL before applying it. Switch the environment connection
back to the least-privilege application user before running the web application.
The idempotent SQL uses temporary migration procedures; executing that script
directly also requires CREATE ROUTINE, ALTER ROUTINE, and EXECUTE privileges for
the migration user. The `dotnet ef database update` path above applies migration
commands directly and does not require those extra procedure privileges.
`./scripts/Start-Demo.ps1` explicitly enables Development initialization and
idempotent demo identity seeding. It requires an already configured MySQL
connection. `./scripts/Start-Demo.ps1 -Sqlite` creates a secret-free local demo.
Production startup never performs schema migration or shared identity seeding.
Each new browser workspace is seeded only when DemoMode is explicitly enabled.

## Optional configured-MySQL smoke test

This smoke test is optional when the owner has not supplied a local secret. It
must use a separate schema, `portfolio_branch_compliance_test`, with the same
charset, collation, and separate migration/application privileges above.

1. Set `ConnectionStrings__DefaultConnection` locally to the test schema using
   the migration identity. Run the migration commands above and verify all
   migrations appear in `__ef_migrations_history`.
2. Set the same environment key to the test application's least-privilege user.
   Run `./scripts/Start-Demo.ps1 -Port 5095` and open `http://localhost:5095`.
3. Sign in as Administrator, inspect the published ten-criterion template and
   current period snapshots, and verify five fictional branches appear.
4. Switch to Branch User in the same browser. Complete Harbor Point's remaining
   responses and attach a synthetic PDF/PNG/JPEG wherever evidence is required.
   Submit, then switch to Administrator and close submissions.
5. Switch to Assessor, give every criterion in every active assessment 80.00,
   and complete scoring. Switch to Administrator and publish provisional results.
6. Switch to Branch User and appeal one Harbor Point criterion. Switch to
   Approver and accept with revised score 90.00 and a decision note.
7. Switch to Administrator and close appeals, then Approver and finalize. With
   ten equal 10% criteria, Harbor Point's provisional score remains 80.00; its
   final score is 81.00 / Good. Other branches remain 80.00 / Good and share rank
   2; Harbor Point has rank 1. Inspect original 80.00 and revised 90.00 history.
8. Export XLSX and PDF, download protected evidence, and verify a private browser
   receives different data and cannot access copied assessment/evidence URLs.

The complete web steps and exports are delivered at their respective milestones;
the milestone plan records whether they have been exercised. A passing SQLite
suite or generated MySQL SQL is not evidence of a live MySQL migration run.

The current implementation environment has MySQL 8.0.46 running but no supplied
connection secret. No live schema or server account has been created or changed
by this build.
