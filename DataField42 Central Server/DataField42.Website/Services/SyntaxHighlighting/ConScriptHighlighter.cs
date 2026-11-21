namespace DataField42.Website.Services.SyntaxHighlighting;

/// <summary>
/// Colours a <c>.con</c> script: control flow, class and method names, variables, numbers and
/// comments. The CSS classes match those in the site stylesheet.
/// </summary>
public static class ConScriptHighlighter
{
    private static readonly HashSet<string> ControlFlow = new(StringComparer.OrdinalIgnoreCase)
    {
        "if", "else", "elseif", "endif",
    };

    private static readonly HashSet<string> Commands = new(StringComparer.OrdinalIgnoreCase)
    {
        "run", "include", "var", "const",
    };

    private static readonly HashSet<string> LogicalOperators = new(StringComparer.Ordinal)
    {
        "==", "!=", ">", "<", ">=", "<=", "&|",
    };

    public static List<HighlightedLine> Highlight(string text) =>
        [.. text.Split('\n').Select(HighlightLine)];

    private static HighlightedLine HighlightLine(string line)
    {
        var highlighted = new HighlightedLine();
        var (className, method, arguments) = SourceLine.Parse(line);

        if (className is null)
            return highlighted;

        string trimmed = className.Trim();

        if (trimmed.Equals("rem", StringComparison.OrdinalIgnoreCase))
        {
            highlighted.Add(line, "cmdComment");
            return highlighted;
        }

        if (ControlFlow.Contains(trimmed))
        {
            highlighted.Add(className, "cmdOperator");
            AddMethod(highlighted, method);
            foreach (var argument in arguments.Where(a => a.Length > 0))
                highlighted.Add(" " + argument, ClassifyConditionArgument(argument));
            return highlighted;
        }

        highlighted.Add(className, Commands.Contains(trimmed) ? "cmdCommand" : "cmdClassName");
        AddMethod(highlighted, method);

        foreach (var argument in arguments)
        {
            if (TryHighlightVector(argument) is { } vector)
            {
                highlighted.AddRange(vector);
                continue;
            }

            if (argument.Length == 0)
                continue;

            highlighted.Add(" " + argument, ClassifyArgument(argument));
        }

        return highlighted;
    }

    private static void AddMethod(HighlightedLine highlighted, string? method)
    {
        if (method is null)
            return;

        highlighted.Add(".", "cmdDot");
        highlighted.Add(method, "cmdMethod");
    }

    /// <summary>
    /// A position, rotation or scale -- up to three slash-separated numbers -- is coloured per
    /// component. An argument where any component is not a number is not a vector.
    /// </summary>
    private static List<SyntaxPart>? TryHighlightVector(string argument)
    {
        var components = argument.Split('/');
        if (components.Length is < 1 or > 3)
            return null;

        var parts = new List<SyntaxPart>(components.Length * 2);
        for (int i = 0; i < components.Length; i++)
        {
            if (!SourceLine.IsNumber(components[i]))
                return null;

            if (i != 0)
                parts.Add(new SyntaxPart("/", "cmdDot"));
            parts.Add(new SyntaxPart((i == 0 ? " " : "") + components[i], "cmdNumber"));
        }

        return parts;
    }

    private static string ClassifyConditionArgument(string argument)
    {
        if (LogicalOperators.Contains(argument))
            return "cmdLogicalOperator";
        return IsVariable(argument) ? "cmdVariable" : "cmdArgument";
    }

    private static string ClassifyArgument(string argument)
    {
        if (argument == "->")
            return "cmdLogicalOperator";
        return IsVariable(argument) ? "cmdVariable" : "cmdArgument";
    }

    private static bool IsVariable(string argument) =>
        argument.StartsWith("v_", StringComparison.OrdinalIgnoreCase) ||
        argument.StartsWith("c_", StringComparison.OrdinalIgnoreCase);
}
