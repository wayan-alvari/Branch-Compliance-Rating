using System.Security.Cryptography;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Branches;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Domain.Scoring;
using BranchCompliance.Domain.Templates;
using BranchCompliance.Domain.Workspaces;
using BranchCompliance.Infrastructure.Identity;
using BranchCompliance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BranchCompliance.Infrastructure.Workspaces;

public sealed class WorkspaceSeeder(ComplianceDbContext db, IClock clock, UserManager<DemoUser> users, WorkspaceFileStore files) : IWorkspaceSeeder
{
    public async Task SeedAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (workspaceId != db.CurrentWorkspaceId) throw new UnauthorizedAccessException("Seed workspace does not match the active scope.");
        if (await db.Branches.AnyAsync(cancellationToken)) return;
        var now = clock.UtcNow;
        var admin = (await users.FindByEmailAsync("admin@compliance.demo"))?.Id ?? throw new InvalidOperationException("Initialize demo identities before workspaces.");
        var branchUser = (await users.FindByEmailAsync("branch@compliance.demo"))!.Id;
        var assessor = (await users.FindByEmailAsync("assessor@compliance.demo"))!.Id;
        var approver = (await users.FindByEmailAsync("approver@compliance.demo"))!.Id;
        var branches = new[]
        {
            new Branch(workspaceId, "DEMO-HP", "Harbor Point", "Coastal", branchUser, admin, now.AddDays(-100)),
            new Branch(workspaceId, "DEMO-MJ", "Maple Junction", "Valley", null, admin, now.AddDays(-100)),
            new Branch(workspaceId, "DEMO-NF", "Northfield", "Highland", null, admin, now.AddDays(-100)),
            new Branch(workspaceId, "DEMO-RS", "Riverside", "Valley", null, admin, now.AddDays(-100)),
            new Branch(workspaceId, "DEMO-SS", "Summit Square", "Highland", null, admin, now.AddDays(-100))
        };
        var template = CreateTemplate(workspaceId, admin, now.AddDays(-100));
        var historical = new AssessmentPeriod(workspaceId, "Completed practice cycle", template, now.AddDays(-90),
            now.AddDays(-80), now.AddDays(-70), now.AddDays(-60), now.AddDays(-50), admin, now.AddDays(-90));
        var previous = branches.Select(branch => new BranchAssessment(historical, branch, assessor, admin, now.AddDays(-90))).ToArray();
        historical.Open(template, previous, admin, now.AddDays(-90));
        foreach (var assessment in previous)
        {
            var responseActor = assessment.BranchUserId ?? "system";
            await RespondAsync(historical, assessment, responseActor, now.AddDays(-85), all: true, cancellationToken);
            assessment.Submit(historical, responseActor, now.AddDays(-85));
        }
        historical.Advance(previous, admin, now.AddDays(-79));
        decimal[] values = [88m, 90m, 90m, 74m, 65m];
        for (var i = 0; i < previous.Length; i++)
        {
            foreach (var criterion in historical.Criteria)
                previous[i].ScoreCriterion(historical, criterion.Id, values[i], "Synthetic review: this score reflects the fictional example response.", assessor, now.AddDays(-75));
            previous[i].CompleteScoring(historical, assessor, now.AddDays(-75));
        }
        historical.Advance(previous, admin, now.AddDays(-69));
        var ordered = historical.Criteria.OrderBy(row => row.CategoryOrder).ThenBy(row => row.Order).ToArray();
        var accepted = previous[0].SubmitAppeal(historical, ordered[0].Id, "Please consider the extra fictional walkway-check explanation.",
            "The example describes an additional routine check.", branchUser, now.AddDays(-65));
        var rejected = previous[0].SubmitAppeal(historical, ordered[1].Id, "Please review the fictional safety-information response once more.",
            "No additional example is available.", branchUser, now.AddDays(-65));
        historical.Advance(previous, admin, now.AddDays(-59));
        previous[0].DecideAppeal(historical, accepted.Id, true, "The additional fictional explanation supports 90.00.", 90m, approver, now.AddDays(-55));
        previous[0].DecideAppeal(historical, rejected.Id, false, "The original fictional response supports the existing score.", null, approver, now.AddDays(-55));
        historical.FinalizeResults(previous, approver, now.AddDays(-54));

        var current = new AssessmentPeriod(workspaceId, "Current practice cycle", template, now.AddDays(-2),
            now.AddDays(7), now.AddDays(14), now.AddDays(21), now.AddDays(28), admin, now.AddDays(-2));
        var active = branches.Select(branch => new BranchAssessment(current, branch, assessor, admin, now.AddDays(-2))).ToArray();
        current.Open(template, active, admin, now.AddDays(-2));
        for (var i = 0; i < active.Length; i++)
        {
            await RespondAsync(current, active[i], i == 0 ? branchUser : "system", now.AddDays(-1), all: i != 0, cancellationToken);
            if (i != 0) active[i].Submit(current, "system", now.AddDays(-1));
        }

        db.Branches.AddRange(branches);
        db.Templates.Add(template);
        db.Periods.AddRange(historical, current);
        db.Assessments.AddRange(previous.Concat(active));
        db.AuditEvents.Add(new AuditEvent(workspaceId, "system", "Workspace created", workspaceId,
            now, "A fresh fictional demo workspace was initialized."));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static AssessmentTemplate CreateTemplate(Guid workspaceId, string actor, DateTime now)
    {
        var template = new AssessmentTemplate(workspaceId, "Everyday Branch Readiness", "Ten original, generic criteria for a fictional portfolio practice assessment.", actor, now);
        var safety = template.AddCategory("Workplace Safety", 10, actor, now);
        var records = template.AddCategory("Record Keeping", 20, actor, now);
        var service = template.AddCategory("Customer Service", 30, actor, now);
        var facility = template.AddCategory("Facility Readiness", 40, actor, now);
        (Guid Category, string Code, string Title, string Guidance, bool Evidence)[] criteria =
        [
            (safety.Id, "WS-01", "Shared walkways", "Describe how the fictional branch checks that shared walkways are ready for use.", true),
            (safety.Id, "WS-02", "Safety information", "Explain how fictional staff can find general safety information.", false),
            (safety.Id, "WS-03", "Practice readiness", "Describe a fictional practice discussion about responding to a routine disruption.", false),
            (records.Id, "RK-01", "Record labeling", "Explain a simple, fictional approach to labeling example records consistently.", false),
            (records.Id, "RK-02", "Record availability", "Describe how an authorized fictional team member finds a sample record.", false),
            (records.Id, "RK-03", "Review reminders", "Explain how a fictional team remembers to review its example records.", false),
            (service.Id, "CS-01", "Service directions", "Describe how a fictional visitor would find clear service directions.", false),
            (service.Id, "CS-02", "Feedback follow-up", "Explain a fictional process for reviewing a sample piece of feedback.", false),
            (facility.Id, "FR-01", "Shared equipment", "Describe a fictional check that shared equipment is available and ready.", true),
            (facility.Id, "FR-02", "Opening readiness", "Explain how a fictional team prepares common areas before opening.", false)
        ];
        for (var i = 0; i < criteria.Length; i++)
        {
            var row = criteria[i];
            template.AddCriterion(row.Category, row.Code, row.Title, row.Guidance, 10m, row.Evidence, i + 1, actor, now);
        }
        template.SetBands([new RatingThreshold("Excellent", 90m), new("Good", 80m), new("Satisfactory", 70m), new("Needs Improvement", 0m)], actor, now);
        template.Publish(actor, now);
        return template;
    }

    private async Task RespondAsync(AssessmentPeriod period, BranchAssessment assessment, string actor, DateTime now,
        bool all, CancellationToken cancellationToken)
    {
        var criteria = period.Criteria.OrderBy(row => row.CategoryOrder).ThenBy(row => row.Order).ToArray();
        foreach (var criterion in all ? criteria : criteria.Take(1))
        {
            var response = assessment.SaveResponse(period, criterion.Id,
                "This fictional branch follows a simple routine and records an example check for this practice assessment.",
                "Synthetic portfolio response; no real branch activity is represented.", actor, now);
            if (criterion.EvidenceRequired)
            {
                var bytes = SyntheticEvidence.Pdf();
                var file = new EvidenceFile(period.WorkspaceId, assessment.Id, response.Id, null, "synthetic-practice-evidence.pdf",
                    "application/pdf", bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)), actor, now);
                assessment.AddEvidence(period, file, actor, now);
                var directory = Directory.CreateDirectory(files.WorkspaceDirectory(period.WorkspaceId));
                await File.WriteAllBytesAsync(Path.Combine(directory.FullName, file.StorageName), bytes, cancellationToken);
            }
        }
    }
}
