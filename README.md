# Branch Compliance & Rating

A portfolio application demonstrating a complete branch assessment workflow:
configure a versioned template, collect protected evidence, assess criteria,
publish provisional scores, decide appeals, and finalize a transparent ranking.

This independent portfolio project was built from scratch using generic
branch-compliance and assessment concepts. It contains no employer source code,
confidential data, proprietary scoring model, branding, or copied business assets.

This is a fictional demonstration, not a regulatory engine or certification
product. Original code is MIT licensed, Copyright (c) 2026 Wayan Alvari.

## Implementation status

Work is tracked in [the milestone plan](Docs/MILESTONES.md). Setup, workflow,
verification, and manual publishing instructions will be completed alongside
their working implementation. No hosting or remote push is part of this build.

## Intended platform

.NET SDK 8.0.424, C# 12, ASP.NET Core MVC/Identity 8.0.30, EF Core 8.0.30,
Pomelo MySQL provider 8.0.3, MySQL Community Server 8.0.46, and AdminLTE 4.1.0.
Node.js 22 and npm 10 prepare locked frontend dependencies. Standard relational
tests use isolated SQLite databases and require no MySQL password.

Real connection details belong in .NET user secrets or environment variables.
Never commit credentials, uploads, databases, local settings, or publish output.

## Demo roles

| Role | Email | Password |
|---|---|---|
| Administrator | admin@compliance.demo | PortfolioDemo123! |
| Branch User | branch@compliance.demo | PortfolioDemo123! |
| Assessor | assessor@compliance.demo | PortfolioDemo123! |
| Approver | approver@compliance.demo | PortfolioDemo123! |

Demo data is isolated per browser and automatically resets after 6 hours of
inactivity. Log out and select another role in the same browser to keep the same
workflow. Use a private window for an independent scenario.

## Scoring

Each contribution is `decimal score × (decimal weight / 100)`. Weights total
exactly 100.00. Only the sum is rounded to two places, using midpoint away from
zero. Rating thresholds are Excellent 90, Good 80, Satisfactory 70, and Needs
Improvement 0. Equal final scores share competition rank (1, 1, 3), with branch
name ordering within a tie.

## Roadmap

Upgrade to .NET 10 before any public deployment remains online past .NET 8 end
of support on 10 November 2026. Revalidate the MySQL provider, reports, locks,
licenses, and complete test suite as part of that upgrade. Hosting is deferred.
