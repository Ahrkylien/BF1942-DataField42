using System.Globalization;
using BF1942.Core.Scripting;

namespace DataField42.Website.Services.MapAnalysis;

/// <summary>
/// Collects the <c>objectTemplate</c>, <c>object</c>, <c>game</c> and <c>console</c> commands the
/// website's map pages are built from, and ignores the rest of the language -- a level's scripts
/// also configure physics, sounds, AI and lightmaps, none of which is reported on here.
/// </summary>
public sealed class LevelScriptHandler(LevelData data) : IScriptCommandHandler
{
    public object? Handle(ScriptCommand command)
    {
        if (command.Is("objectTemplate"))
            return HandleObjectTemplate(command);
        if (command.Is("object"))
            return HandleObject(command);
        if (command.Is("game"))
            return HandleGame(command);
        if (command.Is("console.worldSize") && command.Arguments.Length == 1)
        {
            if (int.TryParse(command.Arguments[0], out int worldSize))
                data.ConsoleWorldSize = worldSize;
            return null;
        }

        return null;
    }

    private object? HandleObjectTemplate(ScriptCommand command)
    {
        var args = command.Arguments;

        if (command.Is("objectTemplate.create"))
        {
            if (args.Length == 2)
                data.ActiveObjectTemplate = data.CreateObjectTemplate(args[0], args[1]) ?? data.ActiveObjectTemplate;
            return null;
        }

        if (command.Is("objectTemplate.active"))
        {
            if (args.Length == 1 && data.GetObjectTemplate(args[0]) is { } referenced)
                data.ActiveObjectTemplate = referenced;
            return null;
        }

        var template = data.ActiveObjectTemplate;
        if (template is null)
            return null;

        if (ScriptCommand.IsMethod(command.Method, "controlPointName") && args.Length == 1)
            template.ControlPointName = args[0];
        else if (ScriptCommand.IsMethod(command.Method, "team") && args.Length == 1)
            template.Team = args[0];
        else if (ScriptCommand.IsMethod(command.Method, "unableToChangeTeam") && args.Length == 1)
            template.UnableToChangeTeam = args[0];
        else if (ScriptCommand.IsMethod(command.Method, "objectTemplate") && args.Length == 2
            && int.TryParse(args[0], out int slot))
            template.SpawnedTemplateNames[slot] = args[1];

        return null;
    }

    private object? HandleObject(ScriptCommand command)
    {
        var args = command.Arguments;

        if (command.Is("object.create"))
        {
            if (args.Length == 1)
                data.ActiveObject = data.CreateObject(args[0]);
            return null;
        }

        if (command.Is("object.active"))
        {
            if (args.Length == 1 && data.GetObject(args[0]) is { } referenced)
                data.ActiveObject = referenced;
            return null;
        }

        var levelObject = data.ActiveObject;
        if (levelObject is null || args.Length != 1)
            return null;

        if (ScriptCommand.IsMethod(command.Method, "absolutePosition"))
            levelObject.AbsolutePosition = Vec3.Parse(args[0]);
        else if (ScriptCommand.IsMethod(command.Method, "rotation"))
            levelObject.Rotation = Vec3.Parse(args[0]);
        else if (ScriptCommand.IsMethod(command.Method, "geometry.scale"))
            levelObject.GeometryScale = Vec3.Parse(args[0]);
        else if (ScriptCommand.IsMethod(command.Method, "OSId"))
            levelObject.OSId = args[0];
        else if (ScriptCommand.IsMethod(command.Method, "team"))
            levelObject.Team = args[0];
        else if (ScriptCommand.IsMethod(command.Method, "name"))
            data.RegisterObjectName(levelObject, args[0]);

        return null;
    }

    private object? HandleGame(ScriptCommand command)
    {
        var args = command.Arguments;
        var game = data.Game;

        if (ScriptCommand.IsMethod(command.Method, "mapId") && args.Length == 1)
            game.MapId = args[0];
        else if (ScriptCommand.IsMethod(command.Method, "activeCombatArea") && args.Length == 4)
            game.ActiveCombatArea = ParseCombatArea(args);
        else if (ScriptCommand.IsMethod(command.Method, "addModPath") && args.Length == 1)
            game.ModPaths.Add(args[0]);
        else if (ScriptCommand.IsMethod(command.Method, "multiplayerBriefingObjectives") && args.Length == 1)
            game.MultiplayerBriefingObjectives = args[0];
        else if (ScriptCommand.IsMethod(command.Method, "objectiveBriefing") && args.Length == 1)
            game.ObjectiveBriefing = args[0];
        else if (ScriptCommand.IsMethod(command.Method, "customGameName"))
        {
            if (args.Length == 1)
                game.CustomGameName = args[0];
            return game.CustomGameName;
        }
        else if (ScriptCommand.IsMethod(command.Method, "customGameVersion"))
        {
            if (args.Length == 1)
                game.CustomGameVersion = args[0];
            return game.CustomGameVersion;
        }

        return null;
    }

    /// <summary>
    /// Combat-area bounds are written as floats in some levels but only ever used as whole-world
    /// coordinates, so they truncate to int the way the reference implementation does.
    /// </summary>
    private static int[]? ParseCombatArea(string[] args)
    {
        var area = new int[4];
        for (int i = 0; i < 4; i++)
        {
            if (!float.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                return null;
            area[i] = (int)value;
        }
        return area;
    }
}
