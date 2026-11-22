namespace DataField42.Website.Services.Admin;

/// <summary>
/// Reads a rule set the way a person would, as one change per file instead of one line per rule.
/// </summary>
/// <remarks>
/// The workstation writes rules for the whole collection, and several of them often describe one
/// file: a file whose contents are new at an old path is a delete and an add, a file moved to a new
/// place with a new date is a move and a date change, and a move whose destination is needed as the
/// source of a later rule is written as two moves through a <c>tmp</c> folder. This puts those back
/// together, and hands each change the findings that concern it.
/// </remarks>
public static class TransferChangeBuilder
{
    private const string TemporaryFolder = "tmp";

    /// <summary>
    /// The changes the rules make, ordered by path (see <see cref="TransferPathComparer"/>), with what <paramref name="issues"/> says about
    /// each and, where a rule sets a date, the date the file has now. A file in the archive that no
    /// rule adds is a change of its own.
    /// </summary>
    /// <param name="lastWrite">The modification date of a file in the collection, or <see langword="null"/> if it is not there.</param>
    public static List<TransferChange> Build(IReadOnlyList<TransferRule> rules, IReadOnlyList<TransferIssue> issues, Func<string, DateTime?> lastWrite)
    {
        var used = new bool[rules.Count];
        var drafts = new List<Draft>();

        // Moves and copies first: they are what a file with a new place is made of, and the dates and
        // adds that follow attach to where they end up.
        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (used[i] || rule.Kind is not (TransferRuleKind.Move or TransferRuleKind.Copy) || rule.DestinationPath is null)
                continue;

            var draft = Start(rule.Kind == TransferRuleKind.Move ? TransferChangeKind.Moved : TransferChangeKind.Copied, rule.DestinationPath, i);
            draft.SourcePath = rule.Path;

            // A move into the temporary folder and the move out of it again are one move.
            if (rule.Kind == TransferRuleKind.Move && rule.DestinationPath.Split('/')[0].Equals(TemporaryFolder, StringComparison.OrdinalIgnoreCase)
                && IndexOf(TransferRuleKind.Move, rule.DestinationPath, from: i + 1) is >= 0 and var back)
            {
                Use(back, draft);
                draft.Path = rules[back].DestinationPath ?? draft.Path;
            }
        }

        // An add where a delete removes the old file is the file replaced by a newer version.
        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i].Kind != TransferRuleKind.Add)
                continue;

            int delete = IndexOf(TransferRuleKind.Delete, rules[i].Path);
            var draft = Start(delete >= 0 ? TransferChangeKind.Replaced : TransferChangeKind.Added, rules[i].Path, delete >= 0 ? delete : i);
            draft.NewDate = rules[i].ModifiedDate;
            if (delete >= 0)
                Use(i, draft);
        }

        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i].Kind == TransferRuleKind.Delete && !used[i])
                Start(TransferChangeKind.Deleted, rules[i].Path, i);
            else if (rules[i].Kind == TransferRuleKind.Unknown)
                Start(TransferChangeKind.NotUnderstood, rules[i].Path, i);
        }

        // A date change belongs to the file it is for, wherever that file came from.
        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i].Kind != TransferRuleKind.ChangeModifiedDate)
                continue;

            var target = drafts.FirstOrDefault(draft => draft.Kind is not (TransferChangeKind.Deleted or TransferChangeKind.Redated or TransferChangeKind.NotUnderstood) && draft.Path == rules[i].Path);
            if (target is null)
                target = Start(TransferChangeKind.Redated, rules[i].Path, i);
            else
                Use(i, target);

            target.NewDate = rules[i].ModifiedDate;
        }

        var archiveIssues = issues.Where(issue => issue.RuleIndex is null && issue.Path.Length > 0).ToList();
        var ruleIssues = issues.Where(issue => issue.RuleIndex is not null).ToLookup(issue => issue.RuleIndex!.Value);

        return
        [
            .. drafts
                .Select(draft => new TransferChange(
                    draft.Kind, draft.Path, draft.SourcePath, draft.NewDate,
                    draft.NewDate is null ? null : lastWrite(draft.SourcePath ?? draft.Path),
                    [.. draft.Rules.SelectMany(index => ruleIssues[index]).OrderByDescending(issue => issue.Severity)]))
                .Concat(archiveIssues.Select(issue => new TransferChange(TransferChangeKind.Added, issue.Path, null, null, null, [issue])))
                .OrderBy(change => change.Path, TransferPathComparer.Instance)
                .ThenBy(change => change.Kind),
        ];

        Draft Start(TransferChangeKind kind, string path, int rule)
        {
            var draft = new Draft(kind, path);
            Use(rule, draft);
            drafts.Add(draft);
            return draft;
        }

        void Use(int rule, Draft draft)
        {
            used[rule] = true;
            draft.Rules.Add(rule);
        }

        int IndexOf(TransferRuleKind kind, string path, int from = 0)
        {
            for (int i = from; i < rules.Count; i++)
            {
                if (!used[i] && rules[i].Kind == kind && rules[i].Path == path)
                    return i;
            }

            return -1;
        }
    }

    /// <summary>
    /// A change while it is being put together.
    /// </summary>
    private sealed class Draft(TransferChangeKind kind, string path)
    {
        public TransferChangeKind Kind { get; } = kind;

        public string Path { get; set; } = path;

        public string? SourcePath { get; set; }

        public DateTime? NewDate { get; set; }

        public List<int> Rules { get; } = [];
    }
}
