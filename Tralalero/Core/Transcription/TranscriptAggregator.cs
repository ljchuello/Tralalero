using System.Text;
using System.Text.RegularExpressions;

namespace Tralalero.Core.Transcription;

/// <summary>Merges phrase-level transcripts without rewriting the model's words.</summary>
public sealed class TranscriptAggregator
{
    private static readonly Regex HorizontalWhitespace = new(@"[ \t]+", RegexOptions.Compiled);
    private static readonly Regex ExcessNewlines = new(@"\n{3,}", RegexOptions.Compiled);

    private readonly StringBuilder _builder = new();

    public string Text => _builder.ToString().Trim();

    public int SegmentCount { get; private set; }

    public void Append(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var normalized = Normalize(text);
        if (normalized.Length == 0)
        {
            return;
        }

        if (_builder.Length > 0)
        {
            var left = _builder[^1];
            var right = normalized[0];
            if (NeedsSpace(left, right))
            {
                _builder.Append(' ');
            }
        }

        _builder.Append(normalized);
        SegmentCount++;
    }

    public void Reset()
    {
        _builder.Clear();
        SegmentCount = 0;
    }

    /// <summary>Returns the tail used as prompt context for the next phrase.</summary>
    public string PromptTail(int maxCharacters = 240)
    {
        var text = Text;
        if (text.Length <= maxCharacters)
        {
            return text;
        }

        return text[^maxCharacters..].TrimStart();
    }

    private static string Normalize(string text)
    {
        var normalized = text.Trim();
        normalized = HorizontalWhitespace.Replace(normalized, " ");
        normalized = ExcessNewlines.Replace(normalized, "\n\n");
        return normalized;
    }

    private static bool NeedsSpace(char left, char right)
    {
        if (char.IsWhiteSpace(left) || char.IsWhiteSpace(right))
        {
            return false;
        }

        if (right is '.' or ',' or ';' or ':' or '!' or '?' or ')' or ']' or '}' or '%' or '…')
        {
            return false;
        }

        if (left is '(' or '[' or '{' or '¿' or '¡' or '"' or '\'' or '“' or '‘')
        {
            return false;
        }

        return true;
    }
}
