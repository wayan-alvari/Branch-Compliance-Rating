using System.Net.Http.Json;
using System.Security.Cryptography;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Domain.Scoring;
using BranchCompliance.Domain.Workspaces;
using BranchCompliance.Infrastructure.Persistence;
using BranchCompliance.Infrastructure.Workspaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Pdf.IO;

namespace BranchCompliance.IntegrationTests;

public sealed class PersistenceTests
{
    private sealed record WorkspaceInfo(Guid WorkspaceId, Guid[] AuditIds);

    private static async Task<Guid> Login(HttpClient browser)
    {
        await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo");
        return (await browser.GetFromJsonAsync<WorkspaceInfo>("/test-workspace"))!.WorkspaceId;
    }

    private static ComplianceDbContext Bind(IServiceProvider services, Guid workspaceId)
    {
        services.GetRequiredService<WorkspaceContext>().Activate(workspaceId);
        return services.GetRequiredService<ComplianceDbContext>();
    }

    [Fact]
    public async Task Seed_persists_complete_generic_scenario_and_is_idempotent()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        var workspaceId = await Login(browser);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = Bind(scope.ServiceProvider, workspaceId);
        Assert.Equal(5, await db.Branches.CountAsync());
        Assert.Equal(1, await db.Templates.CountAsync());
        Assert.Equal(4, await db.TemplateCategories.CountAsync());
        Assert.Equal(10, await db.TemplateCriteria.CountAsync());
        Assert.Equal(100m, (await db.TemplateCriteria.ToListAsync()).Sum(row => row.Weight));
        Assert.Equal(2, await db.Periods.CountAsync());
        Assert.Equal(20, await db.PeriodCriteria.CountAsync());
        Assert.Equal(8, await db.PeriodRatingBands.CountAsync());
        Assert.Equal(10, await db.Assessments.CountAsync());
        Assert.Equal(1, await db.Appeals.CountAsync(row => row.Decision == AppealDecision.Accepted));
        Assert.Equal(1, await db.Appeals.CountAsync(row => row.Decision == AppealDecision.Rejected));
        var completed = await db.Assessments.Where(row => row.State == AssessmentState.Finalized).ToListAsync();
        Assert.Equal(5, completed.Count);
        var harbor = Assert.Single(completed, row => row.BranchCode == "DEMO-HP");
        Assert.Equal(88m, harbor.ProvisionalScore);
        Assert.Equal(88.20m, harbor.FinalScore);
        Assert.Equal(DateTimeKind.Utc, harbor.FinalizedAtUtc!.Value.Kind);
        var ranking = WeightedScoring.Rank(completed.Select(row => new RankingInput(row.Id, row.BranchName, row.FinalScore!.Value)));
        Assert.Equal([1, 1, 3, 4, 5], ranking.Select(row => row.Rank));
        Assert.Equal("Maple Junction", ranking[0].BranchName);
        var activePeriod = await db.Periods.SingleAsync(row => row.Phase == PeriodPhase.SubmissionOpen);
        Assert.Equal(4, await db.Assessments.CountAsync(row => row.PeriodId == activePeriod.Id && row.State == AssessmentState.Submitted));
        Assert.Equal(1, await db.Assessments.CountAsync(row => row.PeriodId == activePeriod.Id && row.State == AssessmentState.InProgress));
        var auditCount = await db.AuditEvents.CountAsync();
        Assert.True(auditCount > 100);
        Assert.Contains(await db.AuditEvents.Select(row => row.Action).ToListAsync(), action => action == "Appeal accepted");
        await scope.ServiceProvider.GetRequiredService<IWorkspaceSeeder>().SeedAsync(workspaceId, CancellationToken.None);
        Assert.Equal(auditCount, await db.AuditEvents.CountAsync());
        Assert.Equal(10, await db.Assessments.CountAsync());
        var fixture = await db.EvidenceFiles.FirstAsync();
        var storage = factory.Services.GetRequiredService<WorkspaceFileStore>();
        var bytes = await File.ReadAllBytesAsync(Path.Combine(storage.WorkspaceDirectory(workspaceId), fixture.StorageName));
        Assert.Equal(fixture.Length, bytes.LongLength);
        Assert.Equal(fixture.Sha256, Convert.ToHexString(SHA256.HashData(bytes)));
        using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        Assert.Equal(1, pdf.PageCount);
    }

    [Fact]
    public async Task Every_domain_table_has_scope_filter_composite_key_and_expiry_clears_all_rows()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        var workspaceId = await Login(browser);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = Bind(scope.ServiceProvider, workspaceId);
            var domainTypes = db.Model.GetEntityTypes().Where(type => typeof(WorkspaceEntity).IsAssignableFrom(type.ClrType)).ToArray();
            Assert.Equal(14, domainTypes.Length);
            foreach (var type in domainTypes)
            {
                Assert.NotNull(type.GetQueryFilter());
                Assert.Contains(type.GetKeys(), key => key.Properties.Select(property => property.Name).SequenceEqual(["WorkspaceId", "Id"]));
                Assert.Matches("^[a-z][a-z0-9_]*$", type.GetTableName()!);
            }
        }
        factory.Clock.Advance(TimeSpan.FromHours(6));
        var replacement = (await browser.GetFromJsonAsync<WorkspaceInfo>("/test-workspace"))!.WorkspaceId;
        Assert.NotEqual(workspaceId, replacement);
        await using var verification = factory.Services.CreateAsyncScope();
        var check = Bind(verification.ServiceProvider, workspaceId);
        Assert.False(await check.Branches.AnyAsync());
        Assert.False(await check.Templates.AnyAsync());
        Assert.False(await check.TemplateCategories.AnyAsync());
        Assert.False(await check.TemplateCriteria.AnyAsync());
        Assert.False(await check.RatingBands.AnyAsync());
        Assert.False(await check.Periods.AnyAsync());
        Assert.False(await check.PeriodCriteria.AnyAsync());
        Assert.False(await check.PeriodRatingBands.AnyAsync());
        Assert.False(await check.Assessments.AnyAsync());
        Assert.False(await check.Responses.AnyAsync());
        Assert.False(await check.Scores.AnyAsync());
        Assert.False(await check.Appeals.AnyAsync());
        Assert.False(await check.EvidenceFiles.AnyAsync());
        Assert.False(await check.AuditEvents.AnyAsync());
        Assert.Equal(4, await check.Users.CountAsync());
    }

    [Fact]
    public async Task Database_foreign_keys_reject_cross_workspace_links_and_optimistic_updates_do_not_lose_history()
    {
        await using var factory = new ComplianceWebFactory();
        using var first = factory.Browser();
        using var second = factory.Browser();
        var a = await Login(first);
        var b = await Login(second);
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var otherScope = factory.Services.CreateAsyncScope();
        var firstDb = Bind(firstScope.ServiceProvider, a);
        var otherDb = Bind(otherScope.ServiceProvider, b);
        var assessmentId = await firstDb.Assessments.Select(row => row.Id).FirstAsync();
        var foreignBranch = await otherDb.Branches.Select(row => row.Id).FirstAsync();
        await Assert.ThrowsAsync<SqliteException>(() => firstDb.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE assessments SET branch_id = {foreignBranch} WHERE id = {assessmentId} AND workspace_id = {a}"));
        await using var concurrentScope = factory.Services.CreateAsyncScope();
        var concurrentDb = Bind(concurrentScope.ServiceProvider, a);
        var branch = await firstDb.Branches.FirstAsync();
        var stale = await concurrentDb.Branches.SingleAsync(row => row.Id == branch.Id);
        var before = await firstDb.AuditEvents.CountAsync();
        branch.Edit("Harbor Example", branch.Region, true, "system", factory.Clock.UtcNow);
        stale.Edit("Stale Example", stale.Region, true, "system", factory.Clock.UtcNow);
        await firstDb.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => concurrentDb.SaveChangesAsync());
        Assert.Equal(before + 1, await firstDb.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Persistence_rejects_edits_to_snapshots_score_history_published_templates_and_final_results()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        var workspaceId = await Login(browser);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = Bind(scope.ServiceProvider, workspaceId);
        var snapshot = await db.PeriodCriteria.FirstAsync();
        db.Entry(snapshot).Property(row => row.Weight).CurrentValue = 11m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var score = await db.Scores.FirstAsync();
        db.Entry(score).Property(row => row.Value).CurrentValue = 0m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var template = await db.Templates.FirstAsync();
        db.Entry(template).Property(row => row.Name).CurrentValue = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var final = await db.Assessments.FirstAsync(row => row.State == AssessmentState.Finalized);
        db.Entry(final).Property(row => row.FinalScore).CurrentValue = 100m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void MySql_model_uses_pinned_provider_decimal_precision_and_utf8mb4_without_a_secret()
    {
        using var db = new ComplianceDesignTimeFactory().CreateDbContext([]);
        var sql = db.Database.GenerateCreateScript();
        Assert.Contains("utf8mb4_0900_ai_ci", sql);
        Assert.Contains("decimal(5,2)", sql);
        Assert.Contains("`period_criteria`", sql);
        Assert.Contains("`workspace_id`", sql);
        Assert.DoesNotContain("Password=", sql);
        var historySql = db.GetService<IHistoryRepository>().GetCreateScript();
        Assert.Contains("`__ef_migrations_history`", historySql);
        Assert.Contains("`migration_id`", historySql);
        Assert.Contains("`product_version`", historySql);
        Assert.Single(db.Database.GetMigrations());
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
