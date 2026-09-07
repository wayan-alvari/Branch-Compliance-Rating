using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using BranchCompliance.Application.Submissions;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Domain.Rules;
using BranchCompliance.Infrastructure.Persistence;
using BranchCompliance.Infrastructure.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BranchCompliance.IntegrationTests;

public sealed class SubmissionTests
{
    private sealed record WorkspaceInfo(Guid WorkspaceId, Guid[] AuditIds);
    private sealed record ScenarioInfo(Guid AssessmentId, Guid RequiredCriterionId, Guid ExistingEvidenceId);

    private static async Task<Guid> Workspace(HttpClient browser)
        => (await browser.GetFromJsonAsync<WorkspaceInfo>("/test-workspace"))!.WorkspaceId;

    private static async Task<T> Read<T>(ComplianceWebFactory factory, Guid workspace,
        Func<ComplianceDbContext, Task<T>> query)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<WorkspaceContext>().Activate(workspace);
        return await query(scope.ServiceProvider.GetRequiredService<ComplianceDbContext>());
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient browser, string formPage, Guid assessmentId,
        Guid responseId, byte[] content, string name, string mediaType)
    {
        using var body = new MultipartFormDataContent();
        body.Add(new StringContent(await ComplianceWebFactory.TokenAsync(browser, formPage)), "__RequestVerificationToken");
        body.Add(new StringContent(responseId.ToString()), "responseId");
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
        body.Add(file, "file", name);
        return await browser.PostAsync($"/Submissions/Upload/{assessmentId}", body);
    }

    private static async Task SwitchRoleAsync(HttpClient browser, string email)
    {
        var logout = await ComplianceWebFactory.PostAsync(browser, "/Dashboard", "/Account/Logout", new());
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await ComplianceWebFactory.LoginAsync(browser, email)).StatusCode);
    }

    [Fact]
    public async Task Evidence_inspection_accepts_three_formats_and_rejects_spoofing_and_oversize_claims()
    {
        await using var factory = new ComplianceWebFactory();
        var storage = factory.Services.GetRequiredService<IEvidenceStorage>();
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF");
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0xFF, 0xD9 };

        var inspectedPdf = await storage.InspectAsync(new MemoryStream(pdf), "../../Sample résumé.pdf",
            "application/pdf", pdf.Length, default);
        Assert.Equal("Sample r_sum_.pdf", inspectedPdf.SafeOriginalName);
        Assert.Equal(64, inspectedPdf.Sha256.Length);
        Assert.Equal("image/png", (await storage.InspectAsync(new MemoryStream(png), "sample.png", "image/png", png.Length, default)).MediaType);
        Assert.Equal("image/jpeg", (await storage.InspectAsync(new MemoryStream(jpeg), "sample.jpeg", "image/jpeg", jpeg.Length, default)).MediaType);
        await Assert.ThrowsAsync<DomainRuleException>(() => storage.InspectAsync(new MemoryStream(Encoding.ASCII.GetBytes("MZ executable")),
            "renamed.pdf", "application/pdf", 13, default));
        await Assert.ThrowsAsync<DomainRuleException>(() => storage.InspectAsync(new MemoryStream(pdf),
            "wrong.png", "application/pdf", pdf.Length, default));
        await Assert.ThrowsAsync<DomainRuleException>(() => storage.InspectAsync(new MemoryStream(pdf),
            "sample.pdf", "text/html", pdf.Length, default));
        await Assert.ThrowsAsync<DomainRuleException>(() => storage.InspectAsync(new MemoryStream(pdf),
            "sample.pdf", "application/pdf", 8L * 1024 * 1024 + 1, default));
    }

    [Fact]
    public async Task Branch_user_completes_uploads_submits_and_role_checked_downloads_evidence()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "branch@compliance.demo");
        var workspace = await Workspace(browser);
        var scenario = await Read(factory, workspace, async db =>
        {
            var assessment = await db.Assessments.Include(row => row.Evidence)
                .SingleAsync(row => row.State == AssessmentState.InProgress && row.BranchUserId != null);
            var period = await db.Periods.Include(row => row.Criteria).SingleAsync(row => row.Id == assessment.PeriodId);
            return new ScenarioInfo(assessment.Id,
                period.Criteria.Single(row => row.Code == "FR-01").Id,
                assessment.Evidence.Single().Id);
        });
        var index = await browser.GetStringAsync("/Submissions");
        Assert.Contains("Harbor Point", index);
        Assert.DoesNotContain("Maple Junction", index);
        var formPage = $"/Submissions/Details/{scenario.AssessmentId}";
        var detail = await browser.GetStringAsync(formPage);
        Assert.Contains("1 / 10 complete", detail);
        Assert.Contains("Workplace Safety", detail);
        Assert.Contains("Facility Readiness", detail);

        var incomplete = await ComplianceWebFactory.PostAsync(browser, formPage, $"/Submissions/Submit/{scenario.AssessmentId}", new());
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.Contains("Every criterion needs a response", await incomplete.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Redirect, (await ComplianceWebFactory.PostAsync(browser, formPage,
            $"/Submissions/Save/{scenario.AssessmentId}", new()
            {
                ["CriterionId"] = scenario.RequiredCriterionId.ToString(),
                ["Answer"] = "A fictional shared-equipment check is recorded before opening.",
                ["Comment"] = "Synthetic branch response."
            })).StatusCode);
        var responseId = await Read(factory, workspace, db => db.Responses
            .Where(row => row.AssessmentId == scenario.AssessmentId && row.PeriodCriterionId == scenario.RequiredCriterionId)
            .Select(row => row.Id).SingleAsync());
        var spoof = await UploadAsync(browser, formPage, scenario.AssessmentId, responseId,
            Encoding.ASCII.GetBytes("MZ executable"), "renamed.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.BadRequest, spoof.StatusCode);
        Assert.Contains("does not match", await spoof.Content.ReadAsStringAsync());

        var pdf = SyntheticEvidence.Pdf();
        Assert.Equal(HttpStatusCode.Redirect, (await UploadAsync(browser, formPage, scenario.AssessmentId, responseId,
            pdf, "../../fictional practice evidence.pdf", "application/pdf")).StatusCode);
        var uploaded = await Read(factory, workspace, db => db.EvidenceFiles
            .SingleAsync(row => row.ResponseId == responseId));
        Assert.Equal("fictional practice evidence.pdf", uploaded.OriginalName);
        Assert.Equal(64, uploaded.Sha256.Length);
        Assert.DoesNotContain("..", uploaded.StorageName);
        var files = factory.Services.GetRequiredService<WorkspaceFileStore>();
        var path = Path.Combine(files.WorkspaceDirectory(workspace), uploaded.StorageName);
        Assert.True(File.Exists(path));
        Assert.Equal(pdf, await File.ReadAllBytesAsync(path));

        var download = await browser.GetAsync($"/Evidence/Download/{uploaded.Id}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.True(download.Headers.CacheControl!.NoStore);
        Assert.Equal("sandbox", download.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal(pdf, await download.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.Redirect, (await ComplianceWebFactory.PostAsync(browser, formPage,
            $"/Submissions/RemoveEvidence/{scenario.AssessmentId}", new() { ["evidenceId"] = uploaded.Id.ToString() })).StatusCode);
        Assert.False(File.Exists(path));
        Assert.False(await Read(factory, workspace, db => db.EvidenceFiles.AnyAsync(row => row.Id == uploaded.Id)));
        Assert.Equal(HttpStatusCode.Redirect, (await UploadAsync(browser, formPage, scenario.AssessmentId, responseId,
            pdf, "replacement.pdf", "application/pdf")).StatusCode);
        var retained = await Read(factory, workspace, db => db.EvidenceFiles.SingleAsync(row => row.ResponseId == responseId));

        var missing = await Read(factory, workspace, async db =>
        {
            var periodId = await db.Assessments.Where(row => row.Id == scenario.AssessmentId).Select(row => row.PeriodId).SingleAsync();
            var criteria = await db.PeriodCriteria.Where(row => row.PeriodId == periodId).OrderBy(row => row.Code).ToArrayAsync();
            var answered = await db.Responses.Where(row => row.AssessmentId == scenario.AssessmentId)
                .Select(row => row.PeriodCriterionId).ToArrayAsync();
            return criteria.Where(row => !answered.Contains(row.Id)).Select(row => row.Id).ToArray();
        });
        foreach (var criterionId in missing)
        {
            var save = await ComplianceWebFactory.PostAsync(browser, formPage, $"/Submissions/Save/{scenario.AssessmentId}", new()
            {
                ["CriterionId"] = criterionId.ToString(),
                ["Answer"] = "This fictional branch records a complete practice response for the criterion.",
                ["Comment"] = "No real branch information is represented."
            });
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }
        Assert.Equal(HttpStatusCode.Redirect,
            (await ComplianceWebFactory.PostAsync(browser, formPage, $"/Submissions/Submit/{scenario.AssessmentId}", new())).StatusCode);
        Assert.Equal(AssessmentState.Submitted,
            await Read(factory, workspace, db => db.Assessments.Where(row => row.Id == scenario.AssessmentId).Select(row => row.State).SingleAsync()));
        var readOnly = await browser.GetStringAsync(formPage);
        Assert.DoesNotContain("<textarea", readOnly);
        Assert.Contains("read only after submission", readOnly);
        var frozenRemoval = await ComplianceWebFactory.PostAsync(browser, formPage,
            $"/Submissions/RemoveEvidence/{scenario.AssessmentId}", new() { ["evidenceId"] = retained.Id.ToString() });
        Assert.Equal(HttpStatusCode.BadRequest, frozenRemoval.StatusCode);

        await SwitchRoleAsync(browser, "assessor@compliance.demo");
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/Evidence/Download/{retained.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Submissions")).StatusCode);
        await SwitchRoleAsync(browser, "approver@compliance.demo");
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.GetAsync($"/Evidence/Download/{retained.Id}")).StatusCode);
        await SwitchRoleAsync(browser, "admin@compliance.demo");
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/Evidence/Download/{retained.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync(formPage)).StatusCode);
        var adminEdit = await ComplianceWebFactory.PostAsync(browser, formPage, $"/Submissions/Save/{scenario.AssessmentId}", new()
        {
            ["CriterionId"] = scenario.RequiredCriterionId.ToString(),
            ["Answer"] = "Administrator must not overwrite this response.",
            ["Comment"] = ""
        });
        Assert.Equal(HttpStatusCode.Forbidden, adminEdit.StatusCode);
    }

    [Fact]
    public async Task Evidence_ids_cannot_cross_browser_workspace_boundaries()
    {
        await using var factory = new ComplianceWebFactory();
        using var first = factory.Browser();
        using var second = factory.Browser();
        await ComplianceWebFactory.LoginAsync(first, "branch@compliance.demo");
        await ComplianceWebFactory.LoginAsync(second, "branch@compliance.demo");
        var firstWorkspace = await Workspace(first);
        var foreignEvidenceId = await Read(factory, firstWorkspace, db => db.EvidenceFiles.Select(row => row.Id).FirstAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"/Evidence/Download/{foreignEvidenceId}")).StatusCode);
    }
}
