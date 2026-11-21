using System.Globalization;

namespace DataField42.Website.Services.MapAnalysis;

/// <summary>
/// A position, rotation or scale as the <c>.con</c> scripts write it: three
/// slash-separated numbers, e.g. <c>128.5/0/-64</c>.
/// </summary>
public readonly record struct Vec3(float X, float Y, float Z)
{
    public static Vec3 Zero => new(0, 0, 0);

    public static Vec3 One => new(1, 1, 1);

    /// <summary>
    /// Parses a script argument. A single number sets all three components (the engine's own
    /// shorthand for a uniform scale); a malformed component leaves its axis at zero.
    /// </summary>
    public static Vec3 Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Zero;

        var parts = value.Split('/');
        if (parts.Length == 1)
            return TryParseComponent(parts[0], out float uniform) ? new Vec3(uniform, uniform, uniform) : Zero;

        TryParseComponent(parts.ElementAtOrDefault(0), out float x);
        TryParseComponent(parts.ElementAtOrDefault(1), out float y);
        TryParseComponent(parts.ElementAtOrDefault(2), out float z);
        return new Vec3(x, y, z);
    }

    private static bool TryParseComponent(string? text, out float value)
    {
        value = 0;
        return text is not null && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public float[] ToArray() => [X, Y, Z];

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{X}/{Y}/{Z}");
}
