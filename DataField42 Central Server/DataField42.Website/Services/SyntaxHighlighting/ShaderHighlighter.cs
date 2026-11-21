namespace DataField42.Website.Services.SyntaxHighlighting;

/// <summary>
/// Colours a <c>.rs</c> shader file, which uses braces and semicolon-terminated statements rather
/// than the script language's class/method lines.
/// </summary>
public static class ShaderHighlighter
{
    public static List<HighlightedLine> Highlight(string text) =>
        [.. text.Split('\n').Select(HighlightLine)];

    private static HighlightedLine HighlightLine(string line)
    {
        var highlighted = new HighlightedLine();
        string trimmed = line.Trim();

        if (trimmed is "{" or "}")
        {
            highlighted.Add(line, "cmdOperator");
            return highlighted;
        }

        if (line.Length == 0)
        {
            highlighted.Add(line, "");
            return highlighted;
        }

        bool endsWithSemicolon = trimmed.EndsWith(';');
        string statement = endsWithSemicolon ? line[..line.LastIndexOf(';')] : line;
        var tokens = statement.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        // A leading tab is part of the file's own indentation and is preserved as written.
        string indent = line.StartsWith('\t') ? "\t" : "";

        for (int i = 0; i < tokens.Length; i++)
            highlighted.Add((i == 0 ? indent : " ") + tokens[i], Classify(tokens[i], i));

        if (endsWithSemicolon)
            highlighted.Add(";", "cmdOperator");

        return highlighted;
    }

    private static string Classify(string token, int index)
    {
        if (index == 0)
            return token.Equals("subshader", StringComparison.OrdinalIgnoreCase) ? "cmdClassName" : "cmdMethod";
        if (SourceLine.IsNumber(token))
            return "cmdNumber";
        return token.Equals("true", StringComparison.OrdinalIgnoreCase) || token.Equals("false", StringComparison.OrdinalIgnoreCase)
            ? "cmdLogicalOperator"
            : "cmdArgument";
    }
}
