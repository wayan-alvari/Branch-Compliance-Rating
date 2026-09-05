# Third-party notices

Original application code is MIT licensed. Dependencies retain their own
licenses. This register is updated at each dependency checkpoint; package lock
files are the authoritative resolved dependency inventory.

## Direct runtime and build dependencies

| Dependency | Exact version | License | Official source | Purpose |
|---|---|---|---|---|
| .NET SDK | 8.0.424 | MIT | https://github.com/dotnet/sdk | Build tooling |
| ASP.NET Core / Identity | 8.0.30 | MIT | https://github.com/dotnet/aspnetcore | MVC and authentication |
| Entity Framework Core | 8.0.30 | MIT | https://github.com/dotnet/efcore | Persistence and migrations |
| dotnet-ef | 8.0.30 | MIT | https://www.nuget.org/packages/dotnet-ef/8.0.30 | Local migration tooling |
| Pomelo.EntityFrameworkCore.MySql | 8.0.3 | MIT | https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql | MySQL provider |
| AdminLTE | 4.1.0 | MIT | https://www.npmjs.com/package/admin-lte/v/4.1.0 | Dashboard shell |
| Bootstrap | 5.3.8 | MIT | https://github.com/twbs/bootstrap | Responsive layout and interactive components |
| @popperjs/core | 2.11.8 | MIT | https://github.com/floating-ui/floating-ui/tree/v2.x | Bootstrap positioning |
| ClosedXML | 0.105.1 | MIT | https://github.com/ClosedXML/ClosedXML | XLSX reports |
| PDFsharp/MigraDoc | 6.2.4 | MIT | https://github.com/empira/PDFsharp | PDF summaries |
| Microsoft.AspNetCore.Mvc.Testing | 8.0.30 | MIT | https://github.com/dotnet/aspnetcore | In-process HTTP integration tests |
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.30 | MIT | https://github.com/dotnet/efcore | Secret-free relational tests and explicit local demo |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | https://github.com/microsoft/vstest | Test host |
| xunit | 2.9.3 | Apache-2.0 | https://github.com/xunit/xunit | Automated assertions and tests |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 | https://github.com/xunit/visualstudio.xunit | Test discovery and runner |

MySQL Community Server 8.0.46 (GPL-2.0, https://dev.mysql.com/) is a separately
installed prerequisite, not bundled with this application. No commercial UI or
reporting components are included.
