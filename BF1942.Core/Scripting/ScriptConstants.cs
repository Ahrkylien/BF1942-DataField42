namespace BF1942.Core.Scripting;

/// <summary>
/// Resolves the script engine's <c>c_</c>-prefixed constants and <c>v_</c>-prefixed variables (set via
/// the <c>const</c>/<c>var</c> commands, or passed into <c>run</c> as <c>v_arg1</c>, <c>v_arg2</c>,
/// ...).
/// 
/// Seeded here with the real engine constants for <c>PhysicsGripFlags</c>. An unresolved
/// <c>c_</c>/<c>v_</c> token is left as its literal string rather than throwing (mods can reference
/// constants this table simply doesn't know about yet).
/// </summary>
public sealed class ScriptConstants
{
    private readonly Dictionary<string, string> _constants;
    private readonly Dictionary<string, string> _variables = new(StringComparer.OrdinalIgnoreCase);

    public ScriptConstants()
    {
        _constants = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // PhysicsGripFlags
            ["c_PGFNoGrip"] = "0",
            ["c_PGFContactGrip"] = "1",
            ["c_PGFRollGrip"] = "2",
            ["c_PGFEngineGrip"] = "4",
            ["c_PGFRollGripWhenOccupied"] = "8",
            ["c_PGFDummyGrip"] = "32",
            ["c_PGFEngineDummyGrip"] = "36", // c_PGFEngineGrip | c_PGFDummyGrip
            ["c_PGFStaticFriction"] = "128",
        };
    }

    public void SetConstant(string name, string value) => _constants[name] = value;

    public void SetVariable(string name, string value) => _variables[name] = value;

    public bool HasVariable(string name) => _variables.ContainsKey(name);

    /// <summary>
    /// Resolves one argument token: <c>v_</c>/<c>c_</c>-prefixed tokens are looked up (case-
    /// insensitively), anything else -- including an unresolved <c>v_</c>/<c>c_</c> token -- passes
    /// through unchanged.
    /// </summary>
    public string Resolve(string token)
    {
        if (token.StartsWith("v_", StringComparison.OrdinalIgnoreCase))
            return _variables.TryGetValue(token, out var v) ? v : token;
        if (token.StartsWith("c_", StringComparison.OrdinalIgnoreCase))
            return _constants.TryGetValue(token, out var c) ? c : token;
        return token;
    }
}
