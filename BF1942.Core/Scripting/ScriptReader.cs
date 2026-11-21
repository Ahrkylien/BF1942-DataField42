using BF1942.FileSystem;

namespace BF1942.Core.Scripting;

/// <summary>
/// Executes the game's script control flow (<c>include</c>/<c>run</c>/<c>var</c>/<c>const</c>/
/// <c>if</c>/<c>elseif</c>/<c>else</c>/<c>endif</c>/<c>beginrem</c>/<c>endrem</c>), delegating every
/// other command to an <see cref="IScriptCommandHandler"/>. Ported from <c>BF42_script.read</c>, but
/// domain-agnostic (the python original hardcodes the objectTemplate/networkableInfo/geometryTemplate/
/// object/game/console dispatch directly in this same method -- split out here into
/// <see cref="IScriptCommandHandler"/> so this class only ever needs to know "script", not
/// "ObjectTemplate").
/// </summary>
public sealed class ScriptReader
{
    private readonly IBfFileSystem _fileSystem;
    private readonly ScriptConstants _constants;
    private readonly IScriptCommandHandler _handler;
    private readonly List<ScriptError> _errors;

    private bool _rem;
    private readonly List<int> _ifStack = []; // 0 = false, 1 = true, 2 = already-true

    public IReadOnlyList<ScriptError> Errors => _errors;

    public ScriptReader(IBfFileSystem fileSystem, ScriptConstants constants, IScriptCommandHandler handler)
        : this(fileSystem, constants, handler, [])
    {
    }

    private ScriptReader(IBfFileSystem fileSystem, ScriptConstants constants, IScriptCommandHandler handler, List<ScriptError> errors)
    {
        _fileSystem = fileSystem;
        _constants = constants;
        _handler = handler;
        _errors = errors;
    }

    /// <summary>
    /// Reads and executes one script file. <paramref name="args"/>, if given, are exposed as
    /// <c>v_arg1</c>, <c>v_arg2</c>, ... (matches the <c>run</c> command's own argument passing).
    /// A missing file is recorded in <see cref="Errors"/> and skipped, not thrown.
    /// </summary>
    public void Read(string path, IReadOnlyList<string>? args = null)
    {
        if (args is not null)
            for (int i = 0; i < args.Count; i++)
                _constants.SetVariable($"v_arg{i + 1}", args[i]);

        if (!_fileSystem.TryReadText(path, out var text))
        {
            _errors.Add(new ScriptError(path, -1, "", "file not found"));
            return;
        }

        var lines = text.Split('\n');
        for (int lineNumber = 0; lineNumber < lines.Length; lineNumber++)
        {
            string line = lines[lineNumber].TrimEnd('\r');
            try
            {
                ExecuteLine(path, line);
            }
            catch (Exception ex)
            {
                _errors.Add(new ScriptError(path, lineNumber, line, ex.Message));
            }
        }
    }

    private void ExecuteLine(string path, string rawLine)
    {
        string line = rawLine.Trim();
        var command = ScriptCommand.Parse(line);
        if (command.ClassName is null)
            return;

        if (!command.Is("var") && !command.Is("const"))
            for (int i = 0; i < command.Arguments.Length; i++)
                command.Arguments[i] = _constants.Resolve(command.Arguments[i]);

        if (command.Is("beginrem"))
        { _rem = true; return; }
        if (command.Is("endrem"))
        { _rem = false; return; }
        if (command.Is("rem") || _rem)
            return;

        int numArgs = command.Arguments.Length;
        if (command.Is("if") && numArgs == 3)
        {
            _ifStack.Add(Evaluate(command.Arguments) ? 1 : 0);
            return;
        }
        if (command.Is("elseif") && numArgs == 3)
        {
            if (_ifStack.Count > 0)
            {
                int top = _ifStack[^1];
                if (top == 0 && Evaluate(command.Arguments))
                    _ifStack[^1] = 1;
                else if (top == 1)
                    _ifStack[^1] = 2;
            }
            return;
        }
        if (command.Is("else") && numArgs == 0)
        {
            if (_ifStack.Count > 0)
            {
                int top = _ifStack[^1];
                if (top == 0)
                    _ifStack[^1] = 1;
                else if (top == 1)
                    _ifStack[^1] = 2;
            }
            return;
        }
        if (command.Is("endif") && numArgs == 0)
        {
            if (_ifStack.Count > 0)
                _ifStack.RemoveAt(_ifStack.Count - 1);
            return;
        }

        if (_ifStack.Any(x => x is 0 or 2))
            return; // inside a false/already-taken branch

        if (command.Method is not null)
        {
            var result = _handler.Handle(command);
            if (command.TargetVariable is not null && result is not null && _constants.HasVariable(command.TargetVariable))
                _constants.SetVariable(command.TargetVariable, result.ToString() ?? "");
            return;
        }

        if (command.Is("include") && numArgs == 1)
        {
            Read(ResolveRelativePath(path, command.Arguments[0]));
        }
        else if (command.Is("run") && numArgs >= 1)
        {
            string runPath = command.Arguments[0];
            if (!Path.HasExtension(runPath))
                runPath += ".con";
            var runArgs = numArgs > 1 ? command.Arguments[1..] : [];
            new ScriptReader(_fileSystem, _constants, _handler, _errors).Read(ResolveRelativePath(path, runPath), runArgs);
        }
        else if (command.Is("var"))
        {
            if (numArgs == 3)
                _constants.SetVariable(command.Arguments[0], command.Arguments[2]);
            else if (numArgs == 1 && !_constants.HasVariable(command.Arguments[0]))
                _constants.SetVariable(command.Arguments[0], "");
        }
        else if (command.Is("const"))
        {
            if (numArgs == 3)
                _constants.SetConstant(command.Arguments[0], command.Arguments[2]);
        }
        else if (command.ClassName.StartsWith("v_", StringComparison.OrdinalIgnoreCase))
        {
            if (numArgs == 2 && _constants.HasVariable(command.ClassName))
                _constants.SetVariable(command.ClassName, command.Arguments[1]);
        }
        else if (command.ClassName.StartsWith("c_", StringComparison.OrdinalIgnoreCase))
        {
            if (numArgs == 2)
                _constants.SetConstant(command.ClassName, command.Arguments[1]);
        }
    }

    /// <summary>
    /// Matches <c>bf42_evaluate</c>: the script engine's <c>if</c>/<c>elseif</c> only ever
    /// supports a case-insensitive <c>==</c> string comparison.
    /// </summary>
    private static bool Evaluate(string[] args) =>
        args[1] == "==" && string.Equals(args[0], args[2], StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Joins <paramref name="relative"/> against the directory of <paramref name="currentPath"/>
    /// and collapses <c>.</c>/<c>..</c> segments -- pure string manipulation, deliberately not
    /// <see cref="Path.GetFullPath(string)"/>: these are virtual archive paths, not real filesystem
    /// paths, and resolving a leading "/" against the current drive/working directory (what
    /// <c>GetFullPath</c> does on Windows) silently produces the wrong path entirely.
    /// </summary>
    private static string ResolveRelativePath(string currentPath, string relative)
    {
        string current = BfPath.Normalize(currentPath);
        string rel = BfPath.Normalize(relative);
        int lastSlash = current.LastIndexOf('/');
        string dir = lastSlash >= 0 ? current[..lastSlash] : "";

        var segments = new List<string>();
        string combined = dir.Length > 0 ? $"{dir}/{rel}" : rel;
        foreach (var segment in combined.Split('/'))
        {
            if (segment is "" or ".")
                continue;
            if (segment == "..")
            { if (segments.Count > 0) segments.RemoveAt(segments.Count - 1); continue; }
            segments.Add(segment);
        }
        return string.Join('/', segments);
    }
}
