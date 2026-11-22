namespace DataField42.Website.Services.Admin;

/// <summary>
/// Checks a rule set against the collection and the archive that comes with it, before anything is
/// changed.
/// </summary>
/// <remarks>
/// The rules are carried out in order, and each depends on what the ones before it did: a move frees
/// the place a later copy needs, and a delete makes a later delete of the same file an error. So the
/// check carries them out on a model of the collection, not on the disk as it is now, and reports
/// what applying them for real would run into. Files an Add rule names only appear when the archive
/// is unpacked, after every rule, so a rule that needs one earlier than that fails too.
/// </remarks>
public sealed class TransferValidator(string modsRoot, Func<string, bool> fileExists, TransferArchive archive)
{
    private readonly List<TransferIssue> _issues = [];

    // What the rules have done to the disk so far, laid over what is really there.
    private readonly HashSet<string> _removed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _created = new(StringComparer.Ordinal);

    /// <summary>
    /// Everything wrong or doubtful about <paramref name="rules"/>, in the order they will be applied.
    /// </summary>
    /// <param name="rules">The rules.</param>
    /// <param name="modsRoot">The mods directory the rules' paths are relative to.</param>
    /// <param name="fileExists">Whether a file, given by its path relative to the mods directory, exists on disk now.</param>
    /// <param name="archive">The uploaded archive.</param>
    public static List<TransferIssue> Validate(IReadOnlyList<TransferRule> rules, string modsRoot, Func<string, bool> fileExists, TransferArchive archive) =>
        new TransferValidator(modsRoot, fileExists, archive).Run(rules);

    /// <summary>
    /// Whether a path from a rule or the archive stays inside the mods directory: it is relative, and
    /// nothing in it climbs out.
    /// </summary>
    public static bool IsInside(string modsRoot, string relativePath)
    {
        if (relativePath.Length == 0 || Path.IsPathRooted(relativePath) || relativePath.StartsWith('/') || relativePath.StartsWith('\\'))
            return false;

        if (relativePath.Replace('\\', '/').Split('/').Contains(".."))
            return false;

        string root = Path.GetFullPath(modsRoot);
        return Path.GetFullPath(Path.Combine(root, relativePath)).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private List<TransferIssue> Run(IReadOnlyList<TransferRule> rules)
    {
        for (int i = 0; i < rules.Count; i++)
            CheckRule(i, rules[i]);

        var adds = rules.Select((rule, index) => (Rule: rule, Index: index)).Where(item => item.Rule.Kind == TransferRuleKind.Add).ToList();
        CheckAdds(adds);
        CheckArchiveEntries(adds.Select(item => item.Rule.Path).ToHashSet(StringComparer.Ordinal));
        return _issues;
    }

    private void CheckRule(int index, TransferRule rule)
    {
        if (rule.Kind == TransferRuleKind.Unknown)
            Add(TransferIssueSeverity.Error, index, rule.Path, $"Not a rule this server understands ('{rule.RawKind}')");
        else if (!IsInside(modsRoot, rule.Path))
            Add(TransferIssueSeverity.Error, index, rule.Path, "The path leaves the mods directory");
        else if (rule.Kind == TransferRuleKind.Delete)
            Delete(index, rule.Path);
        else if (rule.Kind == TransferRuleKind.ChangeModifiedDate && !Exists(rule.Path))
            Add(TransferIssueSeverity.Error, index, rule.Path, "Cannot change its date: it is not there");
        else if (rule.Kind is TransferRuleKind.Move or TransferRuleKind.Copy)
            MoveOrCopy(index, rule);
    }

    private void Delete(int index, string path)
    {
        if (!Exists(path))
        {
            Add(TransferIssueSeverity.Error, index, path, "Cannot delete it: it is not there");
            return;
        }

        _created.Remove(path);
        _removed.Add(path);
    }

    private void MoveOrCopy(int index, TransferRule rule)
    {
        string verb = rule.Kind == TransferRuleKind.Move ? "move" : "copy";

        if (rule.DestinationPath is not { Length: > 0 } destination)
        {
            Add(TransferIssueSeverity.Error, index, rule.Path, $"Cannot {verb} it: the rule has no destination");
            return;
        }

        if (!IsInside(modsRoot, destination))
        {
            Add(TransferIssueSeverity.Error, index, destination, "The destination leaves the mods directory");
            return;
        }

        bool sourceThere = Exists(rule.Path);
        bool destinationFree = !Exists(destination);

        if (!sourceThere)
            Add(TransferIssueSeverity.Error, index, rule.Path, $"Cannot {verb} it: it is not there");
        if (!destinationFree)
            Add(TransferIssueSeverity.Error, index, destination, $"Cannot {verb} {rule.Path} here: a file is already there");
        if (!sourceThere || !destinationFree)
            return;

        if (rule.Kind == TransferRuleKind.Move)
        {
            _created.Remove(rule.Path);
            _removed.Add(rule.Path);
        }

        _removed.Remove(destination);
        _created.Add(destination);
    }

    private void CheckAdds(List<(TransferRule Rule, int Index)> adds)
    {
        if (adds.Count == 0)
            return;

        if (!archive.IsUploaded)
        {
            Add(TransferIssueSeverity.Error, null, "", $"The rules add {TextFormatting.Count(adds.Count, "file")}, but no archive has been uploaded");
            return;
        }

        if (!archive.IsReadable)
        {
            Add(TransferIssueSeverity.Error, null, "", $"The archive cannot be read: {archive.Problem}");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (rule, index) in adds)
        {
            if (!archive.Entries.Contains(rule.Path))
                Add(TransferIssueSeverity.Error, index, rule.Path, "It is not in the archive");
            else if (!seen.Add(rule.Path))
                Add(TransferIssueSeverity.Warning, index, rule.Path, "Another rule adds this file too");
            else if (Exists(rule.Path))
                // The archive is unpacked after every rule, over whatever is there by then.
                Add(TransferIssueSeverity.Warning, index, rule.Path, "A file is already there and will be overwritten");
        }
    }

    private void CheckArchiveEntries(HashSet<string> added)
    {
        if (!archive.IsReadable)
            return;

        foreach (string entry in archive.Entries.Order(StringComparer.Ordinal))
        {
            if (!IsInside(modsRoot, entry))
                Add(TransferIssueSeverity.Error, null, entry, "The archive holds a file that would leave the mods directory");
            else if (!added.Contains(entry))
                Add(TransferIssueSeverity.Warning, null, entry, "In the archive, but no rule adds it; it will be unpacked anyway");
        }
    }

    private bool Exists(string path) => _created.Contains(path) || (!_removed.Contains(path) && fileExists(path));

    private void Add(TransferIssueSeverity severity, int? ruleIndex, string path, string message) =>
        _issues.Add(new TransferIssue(severity, ruleIndex, path, message));
}
