using System.Text;
using DataField42.Website.Data;
using DataField42.Website.Models;
using DataField42.Website.Services;
using DataField42.Website.Services.ServerQuery;
using DataField42.Website.Services.SyntaxHighlighting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataField42.Website.Controllers;

/// <summary>
/// Browses inside a map's archives: the file listing, and the contents of the text files in it.
/// </summary>
public sealed class ArchivesController(DataField42DbContext context, FileDatabase fileDatabase) : Controller
{
    /// <summary>
    /// The extensions worth showing: the game's scripts, shaders and text files. Everything else
    /// in an archive is binary.
    /// </summary>
    private static readonly string[] TextExtensions = ["con", "inc", "txt", "rs", "ssc", "rcm"];

    private static readonly string[] ScriptExtensions = ["con", "inc"];

    [HttpGet("/RFA/{id:int}")]
    public async Task<IActionResult> Index(int id, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "RFA";

        var rfa = await context.Rfas
            .AsNoTracking()
            .Include(r => r.Map)
            .ThenInclude(m => m!.Mod)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (rfa?.Path is null || rfa.Map?.Mod?.FolderName is not { } modFolderName)
            return NotFound();

        var archive = fileDatabase.TryOpenArchive(modFolderName, rfa.Path);

        return View(new RfaPageViewModel
        {
            RfaId = rfa.Id,
            Path = rfa.Path,
            Files = archive is null ? [] : [.. archive.Entries.Select(entry => entry.Name)],
        });
    }

    [HttpGet("/RFA/{id:int}/{*path}")]
    public async Task<IActionResult> File(int id, string path, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "RFA";

        var rfa = await context.Rfas
            .AsNoTracking()
            .Include(r => r.Map)
            .ThenInclude(m => m!.Mod)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (rfa?.Path is null || rfa.Map?.Mod?.FolderName is not { } modFolderName)
            return NotFound();

        var model = new RfaFilePageViewModel
        {
            RfaId = rfa.Id,
            ArchivePath = rfa.Path,
            FilePath = path,
        };

        // A ".backup" suffix is the level editor's own, and what matters is the extension beneath
        // it.
        string pathWithoutBackup = path.EndsWith(".backup", StringComparison.OrdinalIgnoreCase) ? path[..^7] : path;
        string extension = Path.GetExtension(pathWithoutBackup).TrimStart('.').ToLowerInvariant();

        if (!TextExtensions.Contains(extension))
            return View(model with { Text = "Not a text file" });

        var archive = fileDatabase.TryOpenArchive(modFolderName, rfa.Path);
        var entry = archive?.Entries.FirstOrDefault(e => e.Name == path);
        if (archive is null || entry is null)
            return View(model with { Text = "ERROR" });

        string text;
        try
        {
            // Scripts are Windows-1252: the game had no concept of Unicode.
            text = Encodings.Windows1252.GetString(archive.Read(entry));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return View(model with { Text = "Error while extracting file" });
        }

        if (ScriptExtensions.Contains(extension))
            return View(model with { HighlightedLines = ConScriptHighlighter.Highlight(text) });
        if (extension == "rs")
            return View(model with { HighlightedLines = ShaderHighlighter.Highlight(text) });

        return View(model with { Text = text });
    }
}
