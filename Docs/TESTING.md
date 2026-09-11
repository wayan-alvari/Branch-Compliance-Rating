# Verification guide

## Standard quality gate

Use a stable .NET 8 SDK (8.0.100 or later), Node.js 22, and npm 10. From the
repository root, run each command in order:

```powershell
dotnet tool restore
dotnet restore --locked-mode
npm ci
npm audit --omit=dev
dotnet format --verify-no-changes
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
dotnet list package --vulnerable --include-transitive
dotnet list package --deprecated
```

When using the repository-local SDK, first run:

```powershell
. ./scripts/Use-LocalSdk.ps1
```

Locked restore must not modify any `packages.lock.json`. `npm ci` recreates only
the ignored frontend dependency and vendor directories. A successful final run
has no tracked changes.

The handoff baseline is 29 passing unit tests and 65 passing integration tests,
with no skipped tests, build warnings, npm vulnerabilities, or NuGet
vulnerabilities. `dotnet list package --deprecated` reports test-only xUnit
2.9.3 as legacy. It remains pinned to the tested v2 API; moving to xUnit v3 is a
separate major-version migration that requires a full test-host review.

## What the suites prove

The unit suite covers exact template weights, immutable published versions,
period snapshots, score/band boundaries, decimal precision and midpoint
rounding, phase and deadline transitions, appeals, revised scores,
finalization, workspace expiry, and competition-rank ties.

The integration suite uses isolated SQLite relational databases and temporary
evidence directories. It covers all demo logins and policy denials, browser
workspace isolation and expiry, relational workspace guards, template and period
commands, submission and inspected files, scoring, provisional publication,
appeals and decisions, finalization, ranking, filters, XLSX/PDF parsing, audit
scope, response security headers, safe errors, and representative accessibility
semantics. Tests are deterministic and do not require a network or MySQL secret.

To run one project while diagnosing a failure:

```powershell
dotnet test tests/BranchCompliance.UnitTests/BranchCompliance.UnitTests.csproj `
  -c Release --no-build -m:1
dotnet test tests/BranchCompliance.IntegrationTests/BranchCompliance.IntegrationTests.csproj `
  -c Release --no-build -m:1
```

## EF model checks

The committed migration model should match the current domain mappings:

```powershell
dotnet ef migrations has-pending-model-changes `
  --project src/BranchCompliance.Infrastructure `
  --startup-project src/BranchCompliance.Web
```

Generate idempotent SQL for review without contacting a database:

```powershell
dotnet ef migrations script --idempotent `
  --project src/BranchCompliance.Infrastructure `
  --startup-project src/BranchCompliance.Web `
  --output .local/branch-compliance-migrations.sql
```

## Manual browser regression

Start `./scripts/Start-Demo.ps1 -Sqlite` and keep one browser workspace while
switching roles with Log out / switch role.

1. Verify all four credentials open their role-specific dashboard and wrong-role
   URLs return access denied or not found without leaking records.
2. Confirm the Administrator sees five branches, a ten-criterion 100% template,
   an active period, and global historical ranking.
3. Confirm the Branch User sees only Harbor Point, can edit only its open
   submission, and sees only its published/final results and evidence.
4. Confirm the Assessor sees assigned submitted work and protected evidence; the
   Approver sees appeal/finalization context rather than branch drafts.
5. Export a filtered XLSX and one branch PDF. Open both files and verify their
   contents match the visible scope and filter.
6. At 390 px width, inspect the branch submission and scoring forms for keyboard
   reachability, readable labels, and no horizontal page overflow.
7. Keep the browser console open. Navigation and downloads should produce no CSP,
   script, failed-resource, or runtime errors.

The final manual pass used Chrome 152 against real Kestrel. It covered all four
roles, a 7 KB XLSX, a 19 KB PDF, responsive layout, and visual review of the login,
Administrator dashboard, ranking, branch result, and mobile submission.

## Optional MySQL verification

The normal gate deliberately has no database password. When the owner provides a
dedicated local MySQL 8.0.46 test schema and least-privilege users, follow the
end-to-end smoke procedure in [DATABASE.md](DATABASE.md). Record that live result
separately; generated SQL and SQLite tests do not claim a live migration.
