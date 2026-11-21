namespace BF1942.Core.Scripting;

/// <summary>
/// Receives every non-control-flow script command <see cref="ScriptReader"/> parses (everything with
/// a <c>.method</c> part -- <c>objectTemplate.create</c>, <c>networkableInfo.setBasePriority</c>,
/// <c>object.absolutePosition</c>, <c>game.setMapId</c>, ...). Keeps <see cref="ScriptReader"/> itself
/// domain-agnostic: it only knows control flow (include/run/var/const/if/rem), never what an
/// "ObjectTemplate" is.
/// </summary>
public interface IScriptCommandHandler
{
    /// <summary>
    /// The return value, if any -- mirrors the script engine's <c>-> v_target</c> syntax
    /// (e.g. <c>game.customGameName -> v_result</c>), which assigns whatever the command "returns"
    /// into a variable. Return null for commands with no meaningful return value.
    /// </summary>
    object? Handle(ScriptCommand command);
}
