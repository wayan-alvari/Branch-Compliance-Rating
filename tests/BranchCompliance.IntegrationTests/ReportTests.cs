using System.Net;
using System.Net.Http.Json;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Infrastructure.Persistence;
using BranchCompliance.Infrastructure.Workspaces;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Pdf.IO;

namespace BranchCompliance.IntegrationTests;

public sealed class ReportTests
{
    private sealed record WorkspaceInfo(Guid WorkspaceId, Guid[] AuditIds);
    private sealed record SeedInfo(Guid HistoricalPeriodId, Guid CurrentPeriodId, Guid HarborResultId,
        Guid MapleResultId, Guid HarborCurrentId, Guid MapleCurrentId);

    private static async Task<Guid> Workspace(HttpClient browser)
        => (await browser.GetFromJsonAsync<WorkspaceInfo>("/test-workspace"))!.WorkspaceId;

    private static async Task<T> Read<T>(ComplianceWebFactory factory, Guid workspace,
        Func<ComplianceDbContext, Task<T>> query)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<WorkspaceContext>().Activate(workspace);
        return await query(scope.ServiceProvider.GetRequiredService<ComplianceDbContext>());
    }

    private static Task<SeedInfo> Seed(ComplianceWebFactory factory, Guid workspace)
        => Read(factory, workspace, async db =>
        {
            var historical = await db.Periods.SingleAsync(row => row.Phase == PeriodPhase.Finalized);
            var current = await db.Periods.SingleAsync(row => row.Phase == PeriodPhase.SubmissionOpen);
            var results = await db.Assessments.Where(row => row.PeriodId == historical.Id).ToListAsync();
            var active = await db.Assessments.Where(row => row.PeriodId == current.Id).ToListAsync();
            return new SeedInfo(historical.Id, current.Id,
                results.Single(row => row.BranchCode == "DEMO-HP").Id,
                results.Single(row => row.BranchCode == "DEMO-MJ").Id,
                active.Single(row => row.BranchCode == "DEMO-HP").Id,
                active.Single(row => row.BranchCode == "DEMO-MJ").Id);
        });

    private static async Task SwitchRoleAsync(HttpClient browser, string email)
    {
        Assert.Equal(HttpStatusCode.Redirect,
            (await ComplianceWebFactory.PostAsync(browser, "/Dashboard", "/Account/Logout", new())).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await ComplianceWebFactory.LoginAsync(browser, email)).StatusCode);
    }

    [Fact]
    public async Task Final_ranking_uses_competition_ties_and_all_filters()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        Assert.Equal(HttpStatusCode.Redirect,
            (await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo")).StatusCode);
        var workspace = await Workspace(browser);
        var seed = await Seed(factory, workspace);

        var page = await browser.GetStringAsync("/Reports");
        Assert.Contains("Results &amp; ranking", page);
        Assert.Contains("competition ranking uses 1, 1, 3", page);
        Assert.True(page.IndexOf("Maple Junction", StringComparison.Ordinal) <
            page.IndexOf("Northfield", StringComparison.Ordinal));
        Assert.True(page.IndexOf("Northfield", StringComparison.Ordinal) <
            page.IndexOf("Harbor Point", StringComparison.Ordinal));
        Assert.Contains("88.20", page);
        Assert.Contains(">3<", page);

        var valley = await browser.GetStringAsync($"/Reports?PeriodId={seed.HistoricalPeriodId}&Query=Valley");
        Assert.Contains("Maple Junction", valley);
        Assert.Contains("Riverside", valley);
        Assert.DoesNotContain("Northfield", valley);
        Assert.DoesNotContain("Harbor Point", valley);

        var excellent = await browser.GetStringAsync($"/Reports?PeriodId={seed.HistoricalPeriodId}&Rating=Excellent");
        Assert.Contains("Maple Junction", excellent);
        Assert.Contains("Northfield", excellent);
        Assert.DoesNotContain("Harbor Point", excellent);

        var submitted = await browser.GetStringAsync($"/Reports?PeriodId={seed.CurrentPeriodId}&Status=Submitted");
        Assert.Contains("Maple Junction", submitted);
        Assert.DoesNotContain("Harbor Point", submitted);
        Assert.Contains("Not published", submitted);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await browser.GetAsync($"/Reports/Details/{seed.HarborCurrentId}")).StatusCode);
    }

    [Fact]
    public async Task Result_details_show_criterion_contributions_and_appeal_effects_with_role_scope()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "branch@compliance.demo");
        var workspace = await Workspace(browser);
        var seed = await Seed(factory, workspace);

        var branchList = await browser.GetStringAsync("/Reports");
        Assert.Contains("Harbor Point", branchList);
        Assert.DoesNotContain("Maple Junction", branchList);
        Assert.Contains(">3<", branchList);
        var details = WebUtility.HtmlDecode(
            await browser.GetStringAsync($"/Reports/Details/{seed.HarborResultId}"));
        Assert.Contains("criterion contributions", details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Appeal accepted", details);
        Assert.Contains("Original 88.00", details);
        Assert.Contains("final 90.00", details);
        Assert.Contains("+2.00", details);
        Assert.Contains("Appeal rejected", details);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await browser.GetAsync($"/Reports/Details/{seed.MapleResultId}")).StatusCode);

        await SwitchRoleAsync(browser, "assessor@compliance.demo");
        var assessor = await browser.GetStringAsync("/Reports");
        Assert.Contains("Harbor Point", assessor);
        Assert.Contains("Maple Junction", assessor);
        Assert.Equal(HttpStatusCode.OK,
            (await browser.GetAsync($"/Reports/Details/{seed.MapleResultId}")).StatusCode);

        await SwitchRoleAsync(browser, "approver@compliance.demo");
        Assert.Contains("Summit Square", await browser.GetStringAsync("/Reports"));
        await SwitchRoleAsync(browser, "admin@compliance.demo");
        Assert.Contains("Summit Square", await browser.GetStringAsync("/Reports"));
    }

    [Fact]
    public async Task Spreadsheet_and_pdf_exports_are_valid_and_keep_authorized_filters()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo");
        var workspace = await Workspace(browser);
        var seed = await Seed(factory, workspace);

        var spreadsheet = await browser.GetAsync(
            $"/Reports/Spreadsheet?PeriodId={seed.HistoricalPeriodId}&Query=Valley");
        Assert.Equal(HttpStatusCode.OK, spreadsheet.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            spreadsheet.Content.Headers.ContentType!.MediaType);
        Assert.Contains("compliance-results.xlsx", spreadsheet.Content.Headers.ContentDisposition!.ToString());
        Assert.True(spreadsheet.Headers.CacheControl!.NoStore);
        await using (var stream = await spreadsheet.Content.ReadAsStreamAsync())
        using (var workbook = new XLWorkbook(stream))
        {
            var sheet = workbook.Worksheet("Results");
            Assert.Equal("Rank", sheet.Cell(5, 1).GetString());
            Assert.Equal("Maple Junction", sheet.Cell(6, 3).GetString());
            Assert.Equal("Riverside", sheet.Cell(7, 3).GetString());
            Assert.True(sheet.Cell(8, 3).IsEmpty());
            Assert.Equal(90m, sheet.Cell(6, 6).GetValue<decimal>());
        }

        var pdf = await browser.GetAsync($"/Reports/BranchSummary/{seed.HarborResultId}");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.Contains("demo-hp-result.pdf", pdf.Content.Headers.ContentDisposition!.ToString());
        Assert.True(pdf.Headers.CacheControl!.NoStore);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
        using var document = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        Assert.True(document.PageCount > 0);
        Assert.Contains("Harbor Point", document.Info.Title);

        await SwitchRoleAsync(browser, "branch@compliance.demo");
        Assert.Equal(HttpStatusCode.OK,
            (await browser.GetAsync($"/Reports/BranchSummary/{seed.HarborResultId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await browser.GetAsync($"/Reports/BranchSummary/{seed.MapleResultId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await browser.GetAsync($"/Reports/BranchSummary/{seed.HarborCurrentId}")).StatusCode);
        var own = await browser.GetAsync($"/Reports/Spreadsheet?PeriodId={seed.HistoricalPeriodId}");
        await using var ownStream = await own.Content.ReadAsStreamAsync();
        using var ownWorkbook = new XLWorkbook(ownStream);
        Assert.Equal("Harbor Point", ownWorkbook.Worksheet("Results").Cell(6, 3).GetString());
        Assert.True(ownWorkbook.Worksheet("Results").Cell(7, 3).IsEmpty());

        await SwitchRoleAsync(browser, "assessor@compliance.demo");
        Assert.Equal(HttpStatusCode.OK,
            (await browser.GetAsync($"/Reports/BranchSummary/{seed.MapleResultId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await browser.GetAsync($"/Reports/Spreadsheet?PeriodId={seed.HistoricalPeriodId}")).StatusCode);
        await SwitchRoleAsync(browser, "approver@compliance.demo");
        Assert.Equal(HttpStatusCode.OK,
            (await browser.GetAsync($"/Reports/BranchSummary/{seed.MapleResultId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await browser.GetAsync($"/Reports/Spreadsheet?PeriodId={seed.HistoricalPeriodId}")).StatusCode);
    }

    [Fact]
    public async Task Audit_search_applies_each_roles_context_and_actor_filter()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo");
        var workspace = await Workspace(browser);
        var seed = await Seed(factory, workspace);

        var searched = await browser.GetStringAsync("/Audit?Query=Appeal%20accepted&Actor=approver%40compliance.demo");
        Assert.Contains("Appeal accepted", searched);
        Assert.Contains("approver@compliance.demo", searched);
        Assert.DoesNotContain("Appeal rejected", searched);

        await SwitchRoleAsync(browser, "branch@compliance.demo");
        var branch = await browser.GetStringAsync("/Audit");
        Assert.Contains(seed.HarborResultId.ToString(), branch);
        Assert.DoesNotContain(seed.MapleResultId.ToString(), branch);
        Assert.DoesNotContain(seed.MapleCurrentId.ToString(), branch);

        await SwitchRoleAsync(browser, "assessor@compliance.demo");
        var assessor = await browser.GetStringAsync("/Audit");
        Assert.Contains(seed.HarborResultId.ToString(), assessor);
        Assert.Contains(seed.MapleResultId.ToString(), assessor);

        await SwitchRoleAsync(browser, "approver@compliance.demo");
        var approver = await browser.GetStringAsync("/Audit");
        Assert.Contains("Appeal accepted", approver);
        Assert.Contains("Branch result finalized", approver);
        Assert.DoesNotContain("Workspace created", approver);
        Assert.DoesNotContain("Branch response saved", approver);
    }

    [Fact]
    public async Task Reports_require_sign_in_and_reject_foreign_workspace_ids()
    {
        await using var factory = new ComplianceWebFactory();
        using var anonymous = factory.Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/Reports")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/Audit")).StatusCode);

        using var first = factory.Browser();
        using var second = factory.Browser();
        await ComplianceWebFactory.LoginAsync(first, "admin@compliance.demo");
        await ComplianceWebFactory.LoginAsync(second, "admin@compliance.demo");
        var firstWorkspace = await Workspace(first);
        var foreign = await Seed(factory, firstWorkspace);
        Assert.Equal(HttpStatusCode.NotFound,
            (await second.GetAsync($"/Reports/Details/{foreign.HarborResultId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await second.GetAsync($"/Reports?PeriodId={foreign.HistoricalPeriodId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await second.GetAsync($"/Reports/Spreadsheet?PeriodId={foreign.HistoricalPeriodId}")).StatusCode);
    }
}
