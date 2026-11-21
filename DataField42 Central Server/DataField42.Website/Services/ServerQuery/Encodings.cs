using System.Text;

namespace DataField42.Website.Services.ServerQuery;

/// <summary>
/// The legacy code pages the game's network protocol and file formats are written in. .NET ships
/// only Unicode and Latin-1 by default, so the code page provider has to be registered before
/// these can be resolved.
/// </summary>
public static class Encodings
{
    static Encodings()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Windows1252 = Encoding.GetEncoding(1252);
        Windows1251 = Encoding.GetEncoding(1251);
    }

    /// <summary>
    /// Western European, what the game writes by default.
    /// </summary>
    public static Encoding Windows1252 { get; }

    /// <summary>
    /// Cyrillic, what a Russian-language client sends its player name in.
    /// </summary>
    public static Encoding Windows1251 { get; }
}
