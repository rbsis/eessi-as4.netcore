using MimeKit;

namespace AS4.ParserService.Infrastructure;

internal static class MimeTypeMapper
{
    public static string GetExtensionFor(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return string.Empty;
        }

        if (mimeType.Equals("text/xml", StringComparison.OrdinalIgnoreCase))
        {
            return ".xml";
        }

        if (MimeTypes.TryGetExtension(mimeType, out var extension))
        {
            return extension;
        }

        return string.Empty;
    }
}
