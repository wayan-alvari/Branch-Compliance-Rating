using PdfSharp.Fonts;

namespace BranchCompliance.Infrastructure.Reports;

internal sealed class EmbeddedFontResolver : IFontResolver
{
    private const string FaceName = "branch-compliance-basic-regular";
    private const string ResourceName = "BranchCompliance.Basic.Regular.ttf";
    private static readonly object RegistrationLock = new();
    public static EmbeddedFontResolver Instance { get; } = new();

    public static void EnsureRegistered()
    {
        lock (RegistrationLock)
        {
            if (GlobalFontSettings.FontResolver is null)
            {
                GlobalFontSettings.FontResolver = Instance;
            }
            else if (GlobalFontSettings.FontResolver is not EmbeddedFontResolver)
            {
                throw new InvalidOperationException("PDFsharp already has a different font resolver.");
            }
        }
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        => new(FaceName);

    public byte[] GetFont(string faceName)
    {
        if (faceName != FaceName) throw new InvalidOperationException("The requested PDF font face is unavailable.");
        using var stream = typeof(EmbeddedFontResolver).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The embedded PDF font resource is missing.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
