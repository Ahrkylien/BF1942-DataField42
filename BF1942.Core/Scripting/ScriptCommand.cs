namespace BF1942.Core.Scripting;

/// <summary>
/// One parsed line of a script file: "ClassName.method arg1 "quoted arg" arg3 -> v_result". Hand-rolled
/// over <see cref="ReadOnlySpan{Char}"/> rather than the single big regex the python reference used
/// (<c>BF42_command</c>) -- this runs once per line across every script file in a full mod load
/// (thousands of lines), so a per-line <c>Regex.Match</c> would be a real, easily avoidable cost here.
/// </summary>
public sealed class ScriptCommand
{
    private static readonly char[] Separators = [' ', '\t', '\f'];

    public string? ClassName { get; }
    public string? Method { get; }
    public string[] Arguments { get; set; }
    public string? TargetVariable { get; }

    private ScriptCommand(string? className, string? method, string[] arguments, string? targetVariable)
    {
        ClassName = className;
        Method = method;
        Arguments = arguments;
        TargetVariable = targetVariable;
    }

    public static ScriptCommand Parse(ReadOnlySpan<char> line)
    {
        line = TrimLeadingSeparators(line);

        int classEnd = 0;
        while (classEnd < line.Length && line[classEnd] != '.' && !IsSeparator(line[classEnd]))
            classEnd++;
        string? className = classEnd > 0 ? line[..classEnd].ToString() : null;

        var rest = line[classEnd..];
        string? method = null;
        if (rest.Length > 0 && rest[0] == '.')
        {
            rest = rest[1..];
            int methodEnd = 0;
            while (methodEnd < rest.Length && !IsSeparator(rest[methodEnd]))
                methodEnd++;
            if (methodEnd > 0)
                method = rest[..methodEnd].ToString();
            rest = rest[methodEnd..];
        }

        rest = TrimLeadingSeparators(rest);
        var arguments = TokenizeArguments(rest);

        string? targetVariable = null;
        if (arguments.Length > 1 && arguments[^2] == "->" && arguments[^1].StartsWith("v_", StringComparison.OrdinalIgnoreCase))
        {
            targetVariable = arguments[^1];
            arguments = arguments[..^2];
        }

        return new ScriptCommand(className, method, arguments, targetVariable);
    }

    /// <summary>
    /// Matches <c>BF42_command.__eq__</c>: "ClassName.method" where either side can be
    /// blank/"*" to mean "any", and method additionally matches its "set"-prefixed form (the con
    /// script convention where e.g. both <c>.MaxSpeed</c> and <c>.SetMaxSpeed</c> hit the same
    /// handler).
    /// </summary>
    public bool Is(string classAndMethod)
    {
        var parts = classAndMethod.Split('.', 2);
        bool classMatches = parts[0] is "" or "*" || string.Equals(ClassName, parts[0], StringComparison.OrdinalIgnoreCase);
        bool methodMatches = parts.Length == 1 || parts[1] is "" or "*" || IsMethod(Method, parts[1]);
        return classMatches && methodMatches;
    }

    public static bool IsMethod(string? method, string reference) =>
        method is not null &&
        (string.Equals(method, reference, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(method, "set" + reference, StringComparison.OrdinalIgnoreCase));

    private static bool IsSeparator(char c) => c is ' ' or '\t' or '\f';

    private static ReadOnlySpan<char> TrimLeadingSeparators(ReadOnlySpan<char> s)
    {
        int i = 0;
        while (i < s.Length && IsSeparator(s[i]))
            i++;
        return s[i..];
    }

    private static string[] TokenizeArguments(ReadOnlySpan<char> s)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < s.Length)
        {
            while (i < s.Length && IsSeparator(s[i]))
                i++;
            if (i >= s.Length)
                break;

            int start = i;
            if (s[i] == '"')
            {
                i++;
                while (i < s.Length && s[i] != '"')
                    i++;
                if (i < s.Length)
                    i++; // include the closing quote if there was one
            }
            else
            {
                while (i < s.Length && !IsSeparator(s[i]))
                    i++;
            }

            tokens.Add(s[start..i].ToString().Replace("\"", ""));
        }
        return [.. tokens];
    }
}
