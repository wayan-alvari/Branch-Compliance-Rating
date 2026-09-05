using BranchCompliance.Domain.Rules;
using BranchCompliance.Domain.Workspaces;

namespace BranchCompliance.Domain.Assessments;

public sealed class EvidenceFile : WorkspaceEntity
{
    public Guid AssessmentId { get; private set; }
    public Guid? ResponseId { get; private set; }
    public Guid? AppealId { get; private set; }
    public string OriginalName { get; private set; } = "";
    public string StorageName { get; private set; } = "";
    public string MediaType { get; private set; } = "";
    public long Length { get; private set; }
    public string Sha256 { get; private set; } = "";
    public string UploadedBy { get; private set; } = "";
    public DateTime UploadedAtUtc { get; private set; }
    private EvidenceFile() { }

    public EvidenceFile(Guid workspaceId, Guid assessmentId, Guid? responseId, Guid? appealId, string originalName,
        string mediaType, long length, string sha256, string actorId, DateTime now) : base(workspaceId)
    {
        Rule.Require(responseId.HasValue ^ appealId.HasValue, "Evidence must belong to one response or one appeal.");
        Rule.Require(length > 0 && length <= 8 * 1024 * 1024, "Evidence must be between 1 byte and 8 MB.");
        Rule.Require(mediaType is "application/pdf" or "image/jpeg" or "image/png", "Only PDF, JPEG, and PNG evidence is allowed.");
        Rule.Require(sha256.Length == 64 && sha256.All(Uri.IsHexDigit), "Evidence requires a SHA-256 hash.");
        AssessmentId = assessmentId;
        ResponseId = responseId;
        AppealId = appealId;
        OriginalName = Rule.Text(originalName, "File name", 150);
        Rule.Require(!OriginalName.Any(character => char.IsControl(character) || character is '/' or '\\'), "The file name contains invalid characters.");
        MediaType = mediaType;
        Length = length;
        Sha256 = sha256.ToUpperInvariant();
        StorageName = Guid.NewGuid().ToString("N") + (mediaType == "application/pdf" ? ".pdf" : mediaType == "image/png" ? ".png" : ".jpg");
        UploadedBy = Rule.Text(actorId, "Actor", 128);
        Rule.Utc(now);
        UploadedAtUtc = now;
    }
}
