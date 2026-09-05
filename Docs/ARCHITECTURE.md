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
