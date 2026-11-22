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
public static class TransferValidator
{
    /// <summary>
    /// Everything wrong or doubtful about <paramref name="rules"/>.
    /// </summary>
    /// <param name="rules">The rules, in the order they will be applied.</param>
    /// <param name="modsRoot">The mods directory the rules' paths are relative to.</param>
    /// <param name="fileExists">Whether a file, given by its path relative to the mods directory, exists on disk now.</param>
    /// <param name="archive">The uploaded archive.</param>
    public static List<TransferIssue> Validate(
        IReadOnlyList<TransferRule> rules,
        string modsRoot,
        Func<string, bool> fileExists,
        TransferArchive archive)
    {
        var issues = new List<TransferIssue>();

        // What the rules have done to the disk so far, applied over what is really there.
        var removed = new HashSet<string>(StringComparer.Ordinal);
        var created = new HashSet<string>(StringComparer.Ordinal);

        bool Exists(string path) => created.Contains(path) || (!removed.Contains(path) && fileExists(path));

        void Remove(string path)
        {
            created.Remove(path);
            removed.Add(path);
        }

        void Create(string path)
        {
            removed.Remove(path);
            created.Add(path);
        }

        void Error(int index, string path, string message) =>
            issues.Add(new TransferIssue(TransferIssueSeverity.Error, index, path, message));

        var addRules = new List<(int Index, TransferRule Rule)>();

        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];

            if (rule.Kind == TransferRuleKind.Unknown)
            {
                Error(i, rule.Path, $"Not a rule this server understands ('{rule.RawKind}')");
                continue;
            }

            if (!IsInside(modsRoot, rule.Path))
            {
                Error(i, rule.Path, "The path leaves the mods directory");
                continue;
            }

            switch (rule.Kind)
            {
                case TransferRuleKind.Delete:
                    if (!Exists(rule.Path))
                        Error(i, rule.Path, "Cannot delete it: it is not there");
                    else
                        Remove(rule.Path);
                    break;

                case TransferRuleKind.Move:
                case TransferRuleKind.Copy:
                    CheckMoveOrCopy(i, rule);
                    break;

                case TransferRuleKind.ChangeModifiedDate:
                    if (!Exists(rule.Path))
                        Error(i, rule.Path, "Cannot change its date: it is not there");
                    break;

                case TransferRuleKind.Add:
                    addRules.Add((i, rule));
                    break;
            }
        }

        CheckAdds(addRules);
        CheckArchiveEntries(addRules);
        return issues;

        void CheckMoveOrCopy(int index, TransferRule rule)
        {
            string verb = rule.Kind == TransferRuleKind.Move ? "move" : "copy";

            if (rule.DestinationPath is not { Length: > 0 } destination)
            {
                Error(index, rule.Path, $"Cannot {verb} it: the rule has no destination");
                return;
            }

            if (!IsInside(modsRoot, destination))
            {
                Error(index, destination, "The destination leaves the mods directory");
                return;
            }

            bool sourceThere = Exists(rule.Path);
            bool destinationFree = !Exists(destination);

            if (!sourceThere)
                Error(index, rule.Path, $"Cannot {verb} it: it is not there");
            if (!destinationFree)
                Error(index, destination, $"Cannot {verb} {rule.Path} here: a file is already there");

            if (!sourceThere || !destinationFree)
                return;

            if (rule.Kind == TransferRuleKind.Move)
                Remove(rule.Path);

            Create(destination);
        }

        void CheckAdds(List<(int Index, TransferRule Rule)> adds)
        {
            if (adds.Count == 0)
                return;

            if (!archive.IsUploaded)
            {
                issues.Add(new TransferIssue(
                    TransferIssueSeverity.Error,
                    null,
                    "newFiles.zip",
                    $"The rules add {TextFormatting.Count(adds.Count, "file")}, but no archive has been uploaded"));
                return;
            }

            if (!archive.IsReadable)
            {
                issues.Add(new TransferIssue(TransferIssueSeverity.Error, null, "newFiles.zip", $"The archive cannot be read: {archive.Problem}"));
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (index, rule) in adds)
            {
                if (!archive.Entries.Contains(rule.Path))
                {
                    Error(index, rule.Path, "It is not in the archive");
                    continue;
                }

                if (!seen.Add(rule.Path))
                {
                    issues.Add(new TransferIssue(TransferIssueSeverity.Warning, index, rule.Path, "Another rule adds this file too"));
                    continue;
                }

                // The archive is unpacked after every rule, over whatever is there by then.
                if (Exists(rule.Path))
                    issues.Add(new TransferIssue(TransferIssueSeverity.Warning, index, rule.Path, "A file is already there and will be overwritten"));
            }
        }

        void CheckArchiveEntries(List<(int Index, TransferRule Rule)> adds)
        {
            if (!archive.IsReadable)
                return;

            var added = new HashSet<string>(adds.Select(add => add.Rule.Path), StringComparer.Ordinal);

            foreach (string entry in archive.Entries.Order(StringComparer.Ordinal))
            {
                if (!IsInside(modsRoot, entry))
                    issues.Add(new TransferIssue(TransferIssueSeverity.Error, null, entry, "The archive holds a file that would leave the mods directory"));
                else if (!added.Contains(entry))
                    issues.Add(new TransferIssue(TransferIssueSeverity.Warning, null, entry, "In the archive, but no rule adds it; it will be unpacked anyway"));
            }
        }
    }

    /// <summary>
    /// Whether a path from a rule or the archive stays inside the mods directory: it is relative, and
    /// nothing in it climbs out.
    /// </summary>
    public static bool IsInside(string modsRoot, string relativePath)
    {
        if (relativePath.Length == 0 || Path.IsPathRooted(relativePath) || relativePath.StartsWith('/') || relativePath.StartsWith('\\'))
            return false;

        if (relativePath.Replace('\\', '/').Split('/').Any(segment => segment == ".."))
            return false;

        string root = Path.GetFullPath(modsRoot);
        string full = Path.GetFullPath(Path.Combine(root, relativePath));
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
