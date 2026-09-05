using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;

namespace BranchCompliance.Application.Dashboard;

public sealed record PeriodSummary(Guid Id, string Name, PeriodPhase Phase, DateTime SubmissionDeadlineUtc,
    DateTime AssessmentDeadlineUtc, DateTime AppealDeadlineUtc, DateTime FinalizationDeadlineUtc, int CriterionCount);
public sealed record QueueItem(Guid AssessmentId, string BranchName, AssessmentState State, int CompletedResponses,
    int CriterionCount, int ScoredCriteria, int PendingAppeals, decimal? VisibleScore, string? VisibleRating);
public sealed record ResultTeaser(Guid AssessmentId, string BranchName, string PeriodName, decimal Score, string Rating, int Rank);
public sealed record ActivityItem(DateTime AtUtc, string Action, string Details);
public sealed record DashboardModel(string Role, string DisplayName, PeriodSummary? Period, IReadOnlyList<QueueItem> Queue,
    IReadOnlyList<ResultTeaser> History, IReadOnlyList<ActivityItem> Activity, int CompletedCount)
{
    public int SubmittedCount => Queue.Count(row => row.State is not (AssessmentState.NotStarted or AssessmentState.InProgress));
    public int ScoredCount => Queue.Count(row => row.State is AssessmentState.ProvisionallyScored or AssessmentState.AppealPending or AssessmentState.AwaitingFinalization or AssessmentState.Finalized);
    public int PendingAppeals => Queue.Sum(row => row.PendingAppeals);
    public int ReadyToScore => Period?.Phase == PeriodPhase.AssessmentOpen ? Queue.Count(row => row.State == AssessmentState.Submitted) : 0;
}

public static class WorkflowLabels
{
    public static string Phase(PeriodPhase phase) => phase switch
    {
        PeriodPhase.Draft => "Draft",
        PeriodPhase.SubmissionOpen => "Submissions open",
        PeriodPhase.AssessmentOpen => "Assessment open",
        PeriodPhase.AppealOpen => "Appeals open",
        PeriodPhase.FinalReview => "Final review",
        PeriodPhase.Finalized => "Finalized",
        _ => "Unknown"
    };
    public static string Assessment(AssessmentState state) => state switch
    {
        AssessmentState.NotStarted => "Not started",
        AssessmentState.InProgress => "In progress",
        AssessmentState.Submitted => "Submitted",
        AssessmentState.ProvisionallyScored => "Scoring complete",
        AssessmentState.AppealPending => "Appeal pending",
        AssessmentState.AwaitingFinalization => "Awaiting finalization",
        AssessmentState.Finalized => "Finalized",
        _ => "Unknown"
    };
}
