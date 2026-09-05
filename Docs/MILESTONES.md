# Implementation milestones

Repository binding verified: `D:/My Program/Branch-Compliance-Rating`, origin
`https://github.com/wayan-alvari/Branch-Compliance-Rating.git`. Initial tree is
clean and contains only `.gitignore`; existing history has three commits and no
application implementation. Git's existing author identity will be retained.

Each row is completed only after inspection of actual files and passing checks,
then committed separately with the listed message. Local control documents stay
ignored. No deployment, remote push, history rewrite, or Docker is authorized.

| # | Deliverable | Validation | Required checkpoint | Status |
|---|---|---|---|---|
| 0 | License, notices, independence, README, ignore rules | Documentation and prohibited-content scan | `docs: define branch compliance portfolio scope` | Complete |
| 1 | Solution/layers, central pins, SDK/tools, AdminLTE | Locked restore and build | `chore: scaffold branch compliance solution` | Complete |
| 2 | Identity, demo users, policies, login/logout, shell | Authentication tests | `feat(auth): add compliance demo roles` | Complete |
| 3 | Workspace scope and six-hour reset | Isolation/reset tests | `feat(demo): isolate compliance workspaces` | Pending |
| 4 | Domain, mappings/migration, synthetic seeds | Domain/persistence tests | `feat(data): model compliance assessments` | Pending |
| 5 | Dashboards and work queues | View/controller tests | `feat(dashboard): show compliance workflow progress` | Pending |
| 6 | Branches and template versions | Validation/authorization tests | `feat(templates): configure versioned criteria` | Pending |
| 7 | Period snapshots, assignments, phases | Snapshot/transition tests | `feat(periods): open snapshotted assessment periods` | Pending |
| 8 | Responses, protected evidence, submission | Branch/evidence tests | `feat(submissions): collect branch compliance evidence` | Pending |
| 9 | Assessor scoring and weighted results | Decimal/scoring tests | `feat(scoring): calculate provisional branch ratings` | Pending |
| 10 | Publication and criterion appeals | Window/appeal tests | `feat(appeals): add provisional result appeals` | Pending |
| 11 | Appeal decisions and finalization | Authorization/finalization tests | `feat(approval): finalize appealed compliance results` | Pending |
| 12 | Ranking, filters, exports, audit | Tie/filter/export tests | `feat(reports): publish compliance ratings and ranking` | Pending |
| 13 | Security, errors, health, accessibility | All quality gates and workflow regression | `test: harden branch compliance application` | Pending |
| 14 | Screenshots, setup, publish/rollback handoff | Clean-clone and Release publish rehearsal | `docs: complete branch compliance project handoff` | Pending |

## Preflight and decisions

- Installed: .NET SDK 8.0.407 and 10.0.400; Node 22.14.0; npm 10.9.2;
  Git 2.46.0.windows.1; MySQL 8.0.46 with MySQL80 running.
- Required SDK 8.0.424 is being prepared locally in ignored `.local/dotnet`;
  its official SHA-512 must match before extraction/use.
- No connection-string environment variable was supplied. Normal tests will
  use SQLite in-memory. The optional MySQL workflow needs an owner's local
  least-privilege connection string; no password will be guessed or fabricated.
- Decimal midpoint rule: away from zero. Competition ranking: 1, 1, 3.
- One demo branch account represents Harbor Point; other fictional branches
  provide queues and historical ranking. Assignments remain explicit.
- A single-process demo uses scoped services and database transactions for
  concurrent operations; multi-instance hosting needs shared keys and database
  coordination and will be documented if further constraints are required.

## Checkpoint evidence

Append exact validation results here as milestones finish. The final milestone
must also inspect the full history, ignored controls, licenses, exports, mobile
layout, browser errors, and clean working tree.

- Milestone 0: inspected all three initial commits (only `.gitignore` content),
  reviewed all six new/updated documentation files, verified control files and
  representative SDK/evidence/database/local-setting paths are ignored, and
  passed `git diff --check`. History secret-pattern scan found no matches.
- Milestone 1: official SDK archive SHA-512 verified; local SDK reports 8.0.424.
  EF tool 8.0.30 restored; all six package locks generated and locked restore
  passed. Release build passed with zero warnings/errors; format verification
  passed. `npm ci`, asset preparation, and `npm audit --omit=dev` passed with
  zero vulnerabilities. Direct package license metadata was inspected. Windows
  sandbox build processes exited without diagnostics, while the same commands
  passed with approved normal process/network access.
- Milestone 2: formatting and Release build passed (zero warnings/errors); all
  eight SQLite HTTP authentication tests passed. Coverage includes four demo
  logins/logout, every role policy allow/deny pair, missing antiforgery rejection,
  invalid credentials, external return-URL rejection, public credentials/health,
  absent identity-management routes, and repeated identity seeding. Original
  AdminLTE shell uses local scripts/styles and a restrictive CSP. Development
  startup is explicit; shared demo identities use client-based rate limiting
  instead of a shared account lockout that could block other browsers.
