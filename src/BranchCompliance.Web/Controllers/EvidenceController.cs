using BranchCompliance.Application.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BranchCompliance.Web.Controllers;

[Authorize]
public sealed class EvidenceController(SubmissionService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var evidence = await service.DownloadAsync(id, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.ContentSecurityPolicy = "sandbox";
        Response.Headers["X-Download-Options"] = "noopen";
        Response.ContentLength = evidence.Length;
        return File(evidence.Content, evidence.MediaType, evidence.OriginalName, enableRangeProcessing: false);
    }
}
