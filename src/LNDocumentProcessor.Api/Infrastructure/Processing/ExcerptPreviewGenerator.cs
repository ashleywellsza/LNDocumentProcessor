using System.Text;
using LNDocumentProcessor.Api.Application.Abstractions;

namespace LNDocumentProcessor.Api.Infrastructure.Processing;

/// <summary>
/// Simple preview generator: for text-like content it returns a whitespace-
/// collapsed excerpt of the first N characters; for binary content it returns
/// a short descriptor. Deliberately minimal per the assignment — the
/// <see cref="IPreviewGenerator"/> port allows a richer generator later.
/// </summary>
public sealed class ExcerptPreviewGenerator : IPreviewGenerator
{
    private const int MaxExcerptChars = 280;

    public async Task<string> GenerateAsync(
        Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        if (!IsTextLike(contentType))
        {
            return $"[No text preview available for content type '{contentType}'.]";
        }

        // Read only enough bytes to build the excerpt rather than the whole file.
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var buffer = new char[MaxExcerptChars + 1];
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken);

        var text = new string(buffer, 0, read);
        var collapsed = CollapseWhitespace(text);

        if (collapsed.Length > MaxExcerptChars)
        {
            collapsed = collapsed[..MaxExcerptChars].TrimEnd() + "…";
        }

        return collapsed.Length == 0 ? "[Empty document.]" : collapsed;
    }

    private static bool IsTextLike(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        contentType = contentType.Split(';')[0].Trim().ToLowerInvariant();
        return contentType.StartsWith("text/", StringComparison.Ordinal)
            || contentType is "application/json" or "application/xml" or "application/javascript"
            || contentType.EndsWith("+json", StringComparison.Ordinal)
            || contentType.EndsWith("+xml", StringComparison.Ordinal);
    }

    private static string CollapseWhitespace(string input)
    {
        var sb = new StringBuilder(input.Length);
        var lastWasWhitespace = false;
        foreach (var ch in input)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasWhitespace)
                {
                    sb.Append(' ');
                }
                lastWasWhitespace = true;
            }
            else
            {
                sb.Append(ch);
                lastWasWhitespace = false;
            }
        }

        return sb.ToString().Trim();
    }
}
