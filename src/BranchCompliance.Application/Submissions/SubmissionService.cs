using BranchCompliance.Application.Security;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;

namespace BranchCompliance.Application.Submissions;

public sealed record SubmissionListItem(
    Guid AssessmentId,
    string BranchName,
    string PeriodName,
    PeriodPhase Phase,
    AssessmentState State,
    DateTime SubmissionDeadlineUtc,
    int CompletedCriteria,
    int CriterionCount,
    bool CanEdit);

public sealed record SubmissionDetails(
    AssessmentPeriod Period,
    BranchAssessment Assessment,
    string ViewerRole,
    int CompletedCriteria,
    bool CanEdit,
    bool CanSubmit);

public sealed record InspectedEvidence(
    string SafeOriginalName,
    string MediaType,
    long Length,
    string Sha256,
    byte[] Content);

public sealed record EvidenceDownload(Stream Content, string OriginalName, string MediaType, long Length);

public interface IEvidenceStorage
{
    Task<InspectedEvidence> InspectAsync(Stream content, string originalName, string claimedMediaType,
        long claimedLength, CancellationToken cancellationToken);
    Task StoreAsync(Guid workspaceId, string storageName, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
    Task<Stream> OpenVerifiedAsync(Guid workspaceId, string storageName, long expectedLength, string expectedSha256,
        CancellationToken cancellationToken);
    Task DeleteAsync(Guid workspaceId, string storageName, CancellationToken cancellationToken);
}

public interface ISubmissionStore
{
    Task<IReadOnlyList<BranchAssessment>> AssessmentsAsync(string? branchUserId, CancellationToken cancellationToken);
    Task<BranchAssessment?> AssessmentAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssessmentPeriod>> PeriodsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task<AssessmentPeriod?> PeriodAsync(Guid id, CancellationToken cancellationToken);
    Task<EvidenceFile?> EvidenceAsync(Guid id, CancellationToken cancellationToken);
    void Remove(EvidenceFile evidence);
    Task SaveAsync(CancellationToken cancellationToken);
}

public sealed class SubmissionService(
    ISubmissionStore store,
    IEvidenceStorage storage,
    IWorkspaceContext workspace,
    ICurrentActor actors,
    IClock clock)
{
    public async Task<IReadOnlyList<SubmissionListItem>> ListAsync(CancellationToken cancellationToken)
    {
        var actor = Viewer();
        var assessments = await store.AssessmentsAsync(actor.Role == DemoRoles.BranchUser ? actor.Id : null, cancellationToken);
        var periods = (await store.PeriodsAsync(assessments.Select(row => row.PeriodId).Distinct().ToArray(), cancellationToken))
            .ToDictionary(row => row.Id);
        return assessments.Select(assessment =>
        {
            var period = periods[assessment.PeriodId];
            var completed = Completed(period, assessment);
            var editable = CanEdit(actor, period, assessment);
            return new SubmissionListItem(assessment.Id, assessment.BranchName, period.Name, period.Phase,
                assessment.State, period.SubmissionDeadlineUtc, completed, period.Criteria.Count, editable);
        }).OrderByDescending(row => row.CanEdit).ThenByDescending(row => row.SubmissionDeadlineUtc)
            .ThenBy(row => row.BranchName, StringComparer.Ordinal).ToArray();
    }

    public async Task<SubmissionDetails> DetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = Viewer();
        var (period, assessment) = await LoadAsync(id, cancellationToken);
        RequireView(actor, assessment);
        var completed = Completed(period, assessment);
        var editable = CanEdit(actor, period, assessment);
        return new SubmissionDetails(period, assessment, actor.Role, completed, editable,
            editable && completed == period.Criteria.Count && assessment.State == AssessmentState.InProgress);
    }

    public async Task SaveResponseAsync(Guid assessmentId, Guid criterionId, string answer, string comment,
        CancellationToken cancellationToken)
    {
        var actor = Editor();
        var (period, assessment) = await LoadAsync(assessmentId, cancellationToken);
        RequireOwned(actor, assessment);
        assessment.SaveResponse(period, criterionId, answer, comment, actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
    }

    public async Task UploadResponseEvidenceAsync(Guid assessmentId, Guid responseId, Stream content,
        string originalName, string claimedMediaType, long claimedLength, CancellationToken cancellationToken)
    {
        var actor = Editor();
        var (period, assessment) = await LoadAsync(assessmentId, cancellationToken);
        RequireOwned(actor, assessment);
        if (!assessment.Responses.Any(row => row.Id == responseId)) throw new ResourceNotFoundException();
        var inspected = await storage.InspectAsync(content, originalName, claimedMediaType, claimedLength, cancellationToken);
        var evidence = new EvidenceFile(workspace.WorkspaceId, assessment.Id, responseId, null,
            inspected.SafeOriginalName, inspected.MediaType, inspected.Length, inspected.Sha256, actor.Id, clock.UtcNow);
        assessment.AddEvidence(period, evidence, actor.Id, clock.UtcNow);
        await storage.StoreAsync(workspace.WorkspaceId, evidence.StorageName, inspected.Content, cancellationToken);
        try { await store.SaveAsync(cancellationToken); }
        catch
        {
            try { await storage.DeleteAsync(workspace.WorkspaceId, evidence.StorageName, CancellationToken.None); }
            catch (IOException) { }
            throw;
        }
    }

    public async Task RemoveEvidenceAsync(Guid assessmentId, Guid evidenceId, CancellationToken cancellationToken)
    {
        var actor = Editor();
        var (period, assessment) = await LoadAsync(assessmentId, cancellationToken);
        RequireOwned(actor, assessment);
        var evidence = assessment.RemoveEvidence(period, evidenceId, actor.Id, clock.UtcNow);
        store.Remove(evidence);
        await store.SaveAsync(cancellationToken);
        await storage.DeleteAsync(workspace.WorkspaceId, evidence.StorageName, cancellationToken);
    }

    public async Task SubmitAsync(Guid assessmentId, CancellationToken cancellationToken)
    {
        var actor = Editor();
        var (period, assessment) = await LoadAsync(assessmentId, cancellationToken);
        RequireOwned(actor, assessment);
        assessment.Submit(period, actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
    }

    public async Task<EvidenceDownload> DownloadAsync(Guid evidenceId, CancellationToken cancellationToken)
    {
        var actor = actors.Get();
        AccessRules.RequireRole(actor, DemoRoles.All);
        RequireWorkspace();
        var evidence = await store.EvidenceAsync(evidenceId, cancellationToken) ?? throw new ResourceNotFoundException();
        var (_, assessment) = await LoadAsync(evidence.AssessmentId, cancellationToken);
        var allowed = actor.Role switch
        {
            DemoRoles.Administrator => true,
            DemoRoles.BranchUser => assessment.BranchUserId == actor.Id,
            DemoRoles.Assessor => assessment.AssessorId == actor.Id && assessment.State is not AssessmentState.NotStarted and not AssessmentState.InProgress,
            DemoRoles.Approver => evidence.AppealId is not null || EvidenceHasAppealContext(evidence, assessment),
            _ => false
        };
        if (!allowed) throw new AccessDeniedException();
        var content = await storage.OpenVerifiedAsync(workspace.WorkspaceId, evidence.StorageName,
            evidence.Length, evidence.Sha256, cancellationToken);
        return new EvidenceDownload(content, evidence.OriginalName, evidence.MediaType, evidence.Length);
    }

    private Actor Viewer()
    {
        var actor = actors.Get();
        AccessRules.RequireRole(actor, DemoRoles.Administrator, DemoRoles.BranchUser);
        RequireWorkspace();
        return actor;
    }

    private Actor Editor()
    {
        var actor = actors.Get();
        AccessRules.RequireRole(actor, DemoRoles.BranchUser);
        RequireWorkspace();
        return actor;
    }

    private void RequireWorkspace()
    {
        if (workspace.WorkspaceId == Guid.Empty) throw new AccessDeniedException();
    }

    private async Task<(AssessmentPeriod Period, BranchAssessment Assessment)> LoadAsync(Guid assessmentId,
        CancellationToken cancellationToken)
    {
        var assessment = await store.AssessmentAsync(assessmentId, cancellationToken)
            ?? throw new ResourceNotFoundException();
        var period = await store.PeriodAsync(assessment.PeriodId, cancellationToken)
            ?? throw new ResourceNotFoundException();
        return (period, assessment);
    }

    private static void RequireView(Actor actor, BranchAssessment assessment)
    {
        if (actor.Role != DemoRoles.Administrator && assessment.BranchUserId != actor.Id)
            throw new AccessDeniedException();
    }

    private static void RequireOwned(Actor actor, BranchAssessment assessment)
    {
        if (assessment.BranchUserId != actor.Id) throw new AccessDeniedException();
    }

    private bool CanEdit(Actor actor, AssessmentPeriod period, BranchAssessment assessment)
        => actor.Role == DemoRoles.BranchUser && assessment.BranchUserId == actor.Id &&
           period.Phase == PeriodPhase.SubmissionOpen && clock.UtcNow >= period.OpensAtUtc &&
           clock.UtcNow < period.SubmissionDeadlineUtc &&
           assessment.State is AssessmentState.NotStarted or AssessmentState.InProgress;

    private static int Completed(AssessmentPeriod period, BranchAssessment assessment)
        => period.Criteria.Count(criterion => assessment.Responses.Any(response =>
            response.PeriodCriterionId == criterion.Id && response.Answer.Length > 0 &&
            (!criterion.EvidenceRequired || assessment.Evidence.Any(file => file.ResponseId == response.Id))));

    private static bool EvidenceHasAppealContext(EvidenceFile evidence, BranchAssessment assessment)
    {
        var response = assessment.Responses.SingleOrDefault(row => row.Id == evidence.ResponseId);
        return response is not null && assessment.Appeals.Any(row => row.PeriodCriterionId == response.PeriodCriterionId);
    }
}
