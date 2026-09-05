# Architecture and implementation decisions

Four production projects form a modular monolith. Domain owns invariants and
state transitions without a framework dependency. Application owns use cases,
caller authorization, clock/workspace/storage abstractions, and view models.
Infrastructure implements persistence, shared Identity accounts, seeding, file
storage, cleanup, and report rendering. Web composes services and provides thin
MVC controllers and original Razor pages. Tests exercise the invariants and
HTTP authorization using SQLite in-memory relational persistence.

## Reproducibility

SDK 8.0.424 is pinned without roll-forward. Framework/EF packages stay on
8.0.30 and the local EF tool is 8.0.30. Package versions are central; NuGet and
npm locks are committed. Only nuget.org and the official npm registry are used.
`npm ci` copies the AdminLTE production CSS/JS, Bootstrap bundle, and required
license texts to an ignored vendor directory. No theme demo pages or photos are
shipped. Run npm before building/publishing so these files are included.

## Scope and security

Identity users are shared demo identities. Domain records and evidence are
isolated by a protected per-browser workspace cookie. Application authorization
must verify workspace, role, assignment, phase, and record state. EF query
filters and write guards add defense in depth; identifiers alone confer no
authority. Workspace expiry removes domain data and files but preserves shared
Identity accounts. Activity excludes static content, health, and polling.

All clocks use UTC. Deadline boundaries are inclusive at opening and exclusive
at closing. Early administrator phase advances still require readiness; a phase
alone never extends a command's configured deadline. Finalization is an Approver
operation. Snapshot and history rows are immutable after their creation.

## Small explicit choices

- Decimal inputs support two places, scores range 0 through 100, and weights
  must be positive and sum to exactly 100.00. Round the final sum only, using
  `MidpointRounding.AwayFromZero`. Score notes are required below 70.
- Rankings use competition ranks (1, 1, 3) and ordinal branch-name ordering for
  ties. Rating selection uses the rounded overall score.
- The demo branch account is assigned Harbor Point. An Administrator can
  choose branches and the seeded Assessor when configuring periods.
- No public registration, identity-management, password-change, email, external
  integrations, or background messaging is provided.
- MySQL is the deployment database. SQLite is restricted to tests and an
  explicitly enabled development demo; production never silently falls back.
- Evidence is outside public static content and Git, limited to PDF/JPEG/PNG,
  with type/signature validation, random storage names, hashing, and authorized
  attachment streaming. User-supplied HTML is always encoded.

Further implementation decisions and their validation are recorded in the
milestone plan and the final operations documentation.

## Workspace lifecycle

Run a single application process for this demo. A process coordinator holds a
lease throughout each meaningful MVC request and cleanup, preventing reset from
removing in-use rows or evidence. Database transactions make fresh seeding atomic.
Opaque retirement metadata makes requests carrying the same expired cookie
converge on one replacement. Cookies are protected with ASP.NET Data Protection,
HTTP-only, persistent, and secure on HTTPS (always secure outside Development).

Meaningful authenticated visits and commands update exact UTC activity. Database
timestamp writes are throttled to once per minute; an atomic, server-owned UTC
marker in the workspace's private evidence directory preserves intervening
activity across process restart. It contains no credentials or assessment data.
Cleanup runs each minute and flushes observed activity to the database. Static
files, health, and unknown endpoints never create workspaces or extend activity;
there are no polling routes. Login creates the initial scenario, while repeated
anonymous visits do not extend its activity. All reset entry points require
`DemoMode:Enabled=true`.

Expiry is exact at six hours from the last meaningful activity. Request-time
expiry removes only the expired workspace's domain rows and private directory,
then transactionally seeds a new workspace. Shared Identity records survive.
EF filters exclude other workspace rows, write guards reject cross-workspace
changes, and audit rows cannot be modified or deleted through normal persistence.
The dedicated expired-workspace cleanup deletes rows in leaf-to-root order inside
a transaction, preserving MySQL restrictive foreign keys. Multi-process
hosting requires a distributed coordinator and shared storage; it is not a
supported deployment mode for this portfolio implementation.

## Relational model and history

All 14 domain tables have a workspace query filter, a composite workspace/public
ID key, and a workspace foreign key. Relationships between domain rows use
composite foreign keys, so a row cannot reference a different workspace even if
application validation is bypassed. Decimal columns use precision 5, scale 2;
UTC timestamp converters preserve their kind when materializing data.

Aggregates emit safe audit events, persisted atomically with their changes.
Optimistic change versions detect stale updates. Normal persistence rejects
changes to published templates, period snapshots, score revisions, finalized
assessments, submitted responses, decided appeals, and audit events. Only the
dedicated expired-workspace maintenance path bypasses normal history guards.

Branch name/code/region are copied into each assessment assignment so later
branch edits do not relabel historical results. Source criterion IDs in period
snapshots are provenance metadata; all evaluation uses period-owned values.

The migration-history table and its columns also use lowercase names. A narrow
`IHistoryRepository` adapter follows EF's documented extension point and is
tested against pinned Pomelo 8.0.3; revalidate it during the .NET 10/provider
upgrade. EF design-time discovery may resolve registered context options before
calling the factory, so only `EF.IsDesignTime` permits a placeholder connection.
Runtime configuration still requires an owner's real local connection.
