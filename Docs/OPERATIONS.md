# Manual publishing and operations

This document prepares an owner-operated single-process deployment. It does not
deploy, provision a host, alter DNS, or push repository changes. Docker is not
used.

## Prerequisites

- A stable .NET 8 SDK, version 8.0.100 or later, on the build machine.
- Node.js 22 and npm 10 on the build machine.
- MySQL Community Server 8.0.46 with separate application and migration users.
- A supported Windows x64 or Linux x64 host with a local HTTPS reverse proxy.
- Three persistent, access-restricted locations: application files, Data
  Protection keys, and evidence. Evidence and keys must survive releases.

Review [DATABASE.md](DATABASE.md) before publishing. Keep connection strings,
passwords, certificates, keys, database backups, uploads, and completed local
configuration outside Git and outside the release directory.

## Build publish artifacts

Run the quality gate in [TESTING.md](TESTING.md), then create one of these ignored
artifacts from the repository root.

Framework-dependent publish, for a host with the .NET 8 ASP.NET Core runtime:

```powershell
npm ci
dotnet restore --locked-mode
dotnet publish src/BranchCompliance.Web/BranchCompliance.Web.csproj `
  -c Release --no-restore --self-contained false `
  -o publish/framework-dependent
```

Self-contained publish, substituting the host RID when necessary:

```powershell
$branchRid = "linux-x64" # or win-x64
dotnet restore --locked-mode
dotnet publish src/BranchCompliance.Web/BranchCompliance.Web.csproj `
  -c Release --no-restore --self-contained true -r $branchRid `
  -o "publish/self-contained-$branchRid"
```

The build must run `npm ci` first because it copies only the required AdminLTE,
Bootstrap, and Popper production assets into the web project. Publish output is
ignored and must remain outside Git.

Create a reviewed migration bundle for the target RID, or use the reviewed
`dotnet ef database update` path in `DATABASE.md`:

```powershell
dotnet tool restore
$branchBundleSuffix = if ($branchRid -eq "win-x64") { ".exe" } else { "" }
$branchBundle = "publish/migrations/branch-compliance-$branchRid$branchBundleSuffix"
dotnet ef migrations bundle `
  --project src/BranchCompliance.Infrastructure `
  --startup-project src/BranchCompliance.Web `
  --configuration Release --self-contained -r $branchRid `
  --output $branchBundle
```

Store a SHA-256 manifest with the release artifact in the operator's artifact
store. Do not commit executable bundles or manifests containing local paths.

## First-time database and identity preparation

1. Create the schema and separate users shown in `DATABASE.md`.
2. Back up the empty schema, then apply the reviewed migration with the migration
   identity. Confirm the expected rows in `__ef_migrations_history`.
3. Switch to the application identity. On a private machine or a loopback-only
   host session, run `./scripts/Start-Demo.ps1` once with the MySQL connection
   configured. Wait for startup, then stop it. This explicit Development-only
   bootstrap applies any outstanding migration and idempotently creates the four
   shared demo identities. Do not expose that bootstrap process through a public
   proxy.
4. Start the published application in Production. Production startup neither
   changes the schema nor creates shared identities.

The public demo passwords are intentionally documented in the README. Never add
real users or private data to this demo database.

## Production configuration

Set values in the service manager or a protected environment file readable only
by the application account. The examples below show environment-variable names;
replace every placeholder locally.

| Setting | Required value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | Loopback URL such as `http://127.0.0.1:5094` |
| `AllowedHosts` | The public host name, without scheme |
| `ConnectionStrings__DefaultConnection` | Least-privilege MySQL application connection |
| `DemoMode__Enabled` | `true` for isolated six-hour demo workspaces |
| `Database__Initialize` | `false` or unset |
| `DataProtection__KeyPath` | Persistent protected directory outside the release |
| `Evidence__RootPath` | Persistent writable directory outside `wwwroot` and the release |

Do not set `Database__Provider=Sqlite` in Production; startup rejects it. A fixed
`Workspace__Id` is only relevant when demo mode is disabled and an existing
workspace has been provisioned. The portfolio hosting mode uses demo mode.

Grant the application account read/execute access to the release, read/write
access to keys and evidence, and no access to the migration credential. Retain
the same Data Protection keys across restarts and releases or existing workspace
and sign-in cookies become unreadable.

## HTTPS reverse proxy and process

Bind Kestrel to loopback and terminate HTTPS at a local reverse proxy. Forward
the original `Host`, client address, and scheme using `X-Forwarded-For` and
`X-Forwarded-Proto`. The app accepts one forwarded hop from the framework's
default trusted loopback proxies. Keep the proxy on the same host; an external
proxy needs an explicit reviewed trusted-proxy configuration before use.

The proxy should enforce TLS, redirect HTTP to HTTPS, limit request bodies to 10
MB or less, and use a hostname included in `AllowedHosts`. Do not cache dynamic
responses. The application already sends private/no-store and browser security
headers.

Run exactly one application process under a service manager. For a
framework-dependent release the command is:

```text
dotnet BranchCompliance.Web.dll
```

For a self-contained release, run the generated `BranchCompliance.Web`
executable. Configure automatic restart after abnormal exit with a short delay,
capture standard output/error in the platform journal, and rotate logs. Logs must
remain access-restricted; the application avoids supplied response text and
evidence content in its structured expected-failure events.

## Health and release check

After each start or restart, call Kestrel locally and then through HTTPS:

```powershell
Invoke-WebRequest http://127.0.0.1:5094/health -UseBasicParsing
Invoke-WebRequest https://<public-host>/health -UseBasicParsing
```

Both responses must be HTTP 200 with body `Healthy`. The liveness route exposes
no database or stack details and does not create or extend a workspace. Then sign
in with each demo account, verify a private window has different data, and open
one XLSX and PDF export.

## Backups and restore

Back up the MySQL schema, evidence root, and Data Protection key directory as one
release-consistent set. Pause the single application process while taking or
restoring a filesystem-level evidence backup. Encrypt backups, restrict access,
record UTC time and release commit, and test restore on a separate private host.

Workspace cleanup permanently removes database rows and that workspace's
evidence after six hours without meaningful activity. Backups may retain expired
fictional demo data until their normal retention date; document and enforce a
short retention policy.

## Release and rollback sequence

1. Record the current release directory, Git commit, migration ID, and verified
   backup. Stage the new publish output in a new versioned directory.
2. Stop the service, apply the reviewed migration with the migration user, switch
   back to the application credential, and atomically repoint the service to the
   new directory.
3. Start one process and complete the health and role checks above. Keep the old
   application directory until the observation window closes.
4. For an application-only failure with no incompatible migration, stop the
   service, repoint it to the previous directory, restart, and repeat health and
   role checks.
5. If a schema change is incompatible, stop the service and restore the matched
   database, evidence, and key backup before starting the previous application.
   Do not improvise a reverse migration against production data.

Record timestamps, operator, artifact hashes, migration outcome, health results,
and rollback decision in the owner's deployment log.

## Demo cleanup and capacity

Meaningful requests extend a browser workspace; static files, health, unknown
routes, and polling do not. Cleanup checks each minute and removes workspaces
after exactly six inactive hours, including protected evidence. Keep enough disk
space for active uploads and backups, monitor evidence and database growth, and
alert on repeated cleanup or integrity-check failures.

This implementation coordinates cleanup and requests in one process. Multiple
instances require a distributed coordinator plus shared evidence storage and are
outside the supported portfolio deployment.
