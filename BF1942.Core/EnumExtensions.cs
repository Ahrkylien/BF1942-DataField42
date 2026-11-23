using System.ComponentModel;
using System.Reflection;

public static class EnumExtensions
{
    /// <summary>
    /// The label from the value's <see cref="DescriptionAttribute"/>, or its name when it has none.
    /// </summary>
    public static string Description(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        return field?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? value.ToString();
    }
}
