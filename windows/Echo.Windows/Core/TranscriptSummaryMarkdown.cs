namespace Echo_Windows.Core;

public enum SummaryMarkdownBlockKind { Title, Heading, Bullet, Paragraph }

public sealed record SummaryMarkdownBlock(SummaryMarkdownBlockKind Kind, string Text);

public static class TranscriptSummaryMarkdown
{
    public static IReadOnlyList<SummaryMarkdownBlock> Parse(string? markdown)
    {
        var blocks = new List<SummaryMarkdownBlock>();
        var paragraphLines = new List<string>();
        void FlushParagraph()
        {
            if (paragraphLines.Count == 0) return;
            blocks.Add(new(SummaryMarkdownBlockKind.Paragraph, string.Join("\n", paragraphLines)));
            paragraphLines.Clear();
        }

        string normalized = (markdown ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        foreach (string rawLine in normalized.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                FlushParagraph();
            }
            else if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                FlushParagraph();
                blocks.Add(new(SummaryMarkdownBlockKind.Heading, line[4..]));
            }
            else if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushParagraph();
                blocks.Add(new(SummaryMarkdownBlockKind.Title, line[3..]));
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                FlushParagraph();
                blocks.Add(new(SummaryMarkdownBlockKind.Bullet, line[2..]));
            }
            else
            {
                paragraphLines.Add(line);
            }
        }
        FlushParagraph();
        return blocks;
    }
}
