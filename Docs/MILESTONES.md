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
| 3 | Workspace scope and six-hour reset | Isolation/reset tests | `feat(demo): isolate compliance workspaces` | Complete |
| 4 | Domain, mappings/migration, synthetic seeds | Domain/persistence tests | `feat(data): model compliance assessments` | Complete |
| 5 | Dashboards and work queues | View/controller tests | `feat(dashboard): show compliance workflow progress` | Complete |
| 6 | Branches and template versions | Validation/authorization tests | `feat(templates): configure versioned criteria` | Complete |
| 7 | Period snapshots, assignments, phases | Snapshot/transition tests | `feat(periods): open snapshotted assessment periods` | Complete |
| 8 | Responses, protected evidence, submission | Branch/evidence tests | `feat(submissions): collect branch compliance evidence` | Complete |
| 9 | Assessor scoring and weighted results | Decimal/scoring tests | `feat(scoring): calculate provisional branch ratings` | Complete |
| 10 | Publication and criterion appeals | Window/appeal tests | `feat(appeals): add provisional result appeals` | Complete |
| 11 | Appeal decisions and finalization | Authorization/finalization tests | `feat(approval): finalize appealed compliance results` | Complete |
| 12 | Ranking, filters, exports, audit | Tie/filter/export tests | `feat(reports): publish compliance ratings and ranking` | Complete |
| 13 | Security, errors, health, accessibility | All quality gates and workflow regression | `test: harden branch compliance application` | Complete |
| 14 | Screenshots, setup, publish/rollback handoff | Clean-clone and Release publish rehearsal | `docs: complete branch compliance project handoff` | Pending |

## Preflight and decisions

- Installed: .NET SDK 8.0.407 and 10.0.400; Node 22.14.0; npm 10.9.2;
  Git 2.46.0.windows.1; MySQL 8.0.46 with MySQL80 running.
- Required SDK 8.0.424 is installed in ignored `.local/dotnet`; its official
  archive SHA-512 was verified before extraction.
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
- Milestone 3: formatting and Release build passed without warnings/errors;
  two unit and 15 integration tests passed. Tests exercise six-hour boundaries,
  throttled activity and loss of process memory, two-browser isolation, role
  switching, cross-workspace query/write rejection, immutable audit rows,
  concurrent reset convergence, evidence-directory removal, shared Identity
  preservation, excluded health/static activity, and disabled demo reset. The
  private atomic activity marker prevents early expiry after restart; the
  database timestamp is throttled and periodically flushed. Single-process
  hosting and retirement metadata are documented in the architecture notes.
- Milestone 4, domain checkpoint: formatting and Release build passed without
  warnings/errors; 17 unit and 15 integration tests passed. Added original
  template aggregates, immutable published versions with independent draft
  copies, exact weight validation, rating validation/boundaries, decimal-only
  contributions/final rounding, and competition ranking. This coherent domain
  portion is committed separately before period/assessment persistence work;
  milestone 4 remains incomplete until mappings, migration, and seeds pass.
- Milestone 4, workflow-domain checkpoint: Release build and formatting passed;
  24 unit and 15 integration tests passed. Period/assessment transitions,
  immutable snapshots, evidence metadata/completion, score revisions, appeal
  acceptance/rejection, finalization guards, and deadline boundaries are modeled.
  A complete domain scenario proves provisional 70.00 remains unchanged while
  an accepted criterion revision produces final 85.00 / Good, with the original
  60.00 criterion revision retained. Persistence and the public workflow remain
  subsequent work; this checkpoint does not claim their completion.
- Milestone 4 complete: formatting verification, Release build (zero warnings
  and errors), 24 unit tests, and 20 SQLite integration tests passed. Full seed
  persistence, PDF fixture parsing/hash checks, every domain table's isolation
  and reset, composite foreign keys, optimistic conflicts, and immutable records
  are tested. Initial MySQL migration and idempotent SQL generated successfully;
  both test and EF tooling report no pending model changes. Migration history
  uses lowercase table/column names. EF discovery required a design-time-only
  placeholder when resolving registered options; runtime still requires local
  configuration. No MySQL connection secret is available, so live migration and
  the optional smoke workflow are documented in DATABASE.md, not claimed as run.
- Milestone 5: formatting and Release build passed; 25 unit and 25 integration
  tests passed. Four role-specific dashboards display deadline/phase progress,
  scoped queues, response/evidence completion, final result previews with global
  competition rank, and scoped recent activity. HTTP/store tests prove Branch
  User sees only Harbor Point, an unassigned Assessor sees an empty dashboard,
  unknown roles/wrong workspace queries fail, and unpublished values are hidden.
  Application actor/access services and centralized safe expected-error handling
  provide the authorization foundation for the remaining command pages.
- Milestone 6: formatting and Release build passed without warnings or errors;
  26 unit tests and 31 SQLite integration tests passed. Administrator-only HTTP
  tests create, edit, deactivate, and assign branches; create and validate draft
  categories, criteria, weights, and rating bands; publish an immutable version;
  and create an independent successor draft. Role denial, crafted posts,
  cross-browser workspace isolation, historical branch snapshots, duplicate
  codes, unsafe deletion, and draft mutation rules are covered. EF tooling
  reports no pending model changes after recording explicit public-ID generation.
- Milestone 7: formatting and Release build passed without warnings or errors;
  27 unit tests and 36 SQLite integration tests passed. Administrator-only HTTP
  coverage creates a UTC schedule from one published template, assigns an active
  branch and Assessor, rejects duplicate assignments and an empty opening, opens
  a period, and advances only after every submission is ready. Snapshot checks
  compare source IDs, category/order, code, title, guidance, weight, evidence
  rules, and rating bands. Role denial, cross-browser isolation, deadline order,
  and premature transition errors are covered. The period page shows all phase
  deadlines, assignment progress, readiness guidance, and the immutable snapshot.
- Milestone 8: formatting and Release build passed without warnings or errors;
  28 unit tests and 39 SQLite integration tests passed. The Branch User HTTP
  workflow saves all criterion drafts, reports evidence-aware completion, rejects
  incomplete submission, accepts protected evidence, removes a draft attachment,
  and freezes responses and files after submission. File tests cover PDF/JPEG/PNG
  magic bytes, extension and claimed-type agreement, exact length, the 8 MB cap,
  SHA-256, sanitized traversal-style names, randomized paths, and three-file
  limits. Downloads verify stored length/hash and set attachment-only, no-store,
  `sandbox`, and `nosniff` headers. Tests prove Branch User ownership, Assessor
  access only after submission, Administrator read-only recovery, restricted
  Approver context, cross-browser isolation, and storage outside `wwwroot`.
- Milestone 9: formatting and Release build passed without warnings or errors;
  28 unit tests and 44 SQLite integration tests passed. The Assessor-only queue
  presents assigned responses and protected evidence beside criterion score and
  note forms. Tests enforce the 0.00-100.00 range, two decimal places, a required
  note below 70.00, complete-criterion readiness, assignment/workspace scope, and
  immutable draft revisions (60.00 then 90.00). Ten 10% criteria produce a tested
  81.00 / Good preview using decimal contributions and final-only rounding. The
  stored provisional result remains hidden from the Branch User in AssessmentOpen,
  premature publication fails, and the same result appears only after the
  Administrator advances all completed assessments into AppealOpen.
- Milestone 10: formatting and Release build passed without warnings or errors;
  29 unit tests and 50 SQLite integration tests passed. The Branch User sees an
  owned provisional total, rating, criterion scores, notes, decimal contributions,
  and the UTC appeal deadline only after publication. HTTP tests submit a required
  reason and optional clarification, retain the original 60.00 score, reject a
  duplicate criterion appeal, and persist pending state/audit history. Protected
  appeal evidence uses the same inspected storage boundary, supports removal while
  pending, and is downloadable in Branch, assigned Assessor, and Approver appeal
  context. Tests cover the three-file limit, post-decision removal lock, unpublished
  result hiding, all non-Branch command denials, and cross-browser isolation.
- Milestone 11: formatting and Release build passed without warnings or errors;
  29 unit tests and 55 SQLite integration tests passed. The Approver-only queue
  reports pending appeals and per-branch readiness, while its review presents the
  immutable original score/note, branch response and evidence, appeal reason,
  clarification, and appeal evidence side by side. Tests reject acceptance without
  a revised score, rejection with a revised score, duplicate decisions, premature
  and repeated finalization, wrong roles, and foreign workspaces. An accepted
  80.00-to-100.00 criterion change creates attributed revision 2; a rejected appeal
  adds no revision. Finalization preserves provisional 80.00, recomputes final
  82.00 / Good, freezes all five results, audits the transition, and exposes the
  final value to the owning Branch User.
- Milestone 12: formatting and Release build passed without warnings or errors;
  29 unit tests and 60 SQLite integration tests passed. The results page computes
  global competition rank before applying role scope, orders tied branches by
  name, and filters one period by branch/code/region, rating, and workflow status.
  Result detail shows immutable provisional and final criterion revisions,
  decimal contributions, accepted/rejected appeal effects, and the final rank.
  The filtered XLSX was reopened with ClosedXML and its rows, numeric score, MIME
  type, attachment name, and no-store policy were verified. The branch PDF was
  reopened with PDFsharp and uses an embedded, OFL-licensed Basic font (SHA-256
  `077F7245F6459045495B1CA0493F2B426C421D2112D10B48A38FF8858A07397A`) for
  host-independent rendering. Tests cover all four export scopes, unpublished
  result rejection, foreign-workspace IDs, anonymous redirects, audit text and
  actor search, and Administrator/own/assigned/approval audit contexts.
- Milestone 13: every quality-gate command completed. Locked restore and npm
  install/audit passed, with zero npm or NuGet vulnerabilities. The deprecation
  audit reports only test-only xUnit 2.9.3 as legacy; it is deliberately retained
  on the tested v2 line instead of silently crossing a major-version boundary.
  Formatting verification and the Release build passed with zero warnings or
  errors; 29 unit tests and 65 SQLite integration tests passed. New regression
  coverage verifies minimal liveness, secure/no-store cookies and headers,
  bounded forms and uploads, one-hop trusted proxy forwarding, safe status/error
  responses, non-sensitive structured logging, accessible landmarks, labels,
  captions, unique IDs, responsive breakpoints, and representative pages for all
  four roles. Chrome 152 exercised the real Kestrel site through all four role
  logins, XLSX and PDF downloads, and a 390-by-844 branch form. It reported no
  console errors or horizontal overflow. Visual review confirmed the fixed light
  theme, readable desktop/mobile layouts, and the rendered template version. No
  MySQL credential is available, so the optional live MySQL smoke test remains
  documented rather than claimed.
