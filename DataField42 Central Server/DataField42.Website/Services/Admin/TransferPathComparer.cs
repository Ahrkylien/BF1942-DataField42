namespace DataField42.Website.Services.Admin;

/// <summary>
/// Orders paths the way a file browser lists a folder: the files in a folder first, then its
/// subfolders, each in alphabetical order.
/// </summary>
public sealed class TransferPathComparer : IComparer<string>
{
    public static TransferPathComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        var left = (x ?? "").Split('/');
        var right = (y ?? "").Split('/');

        for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            int byName = StringComparer.OrdinalIgnoreCase.Compare(left[i], right[i]);
            if (byName == 0)
                continue;

            // At this level one path ends in a file and the other goes into a folder.
            bool leftIsFile = i == left.Length - 1;
            bool rightIsFile = i == right.Length - 1;
            if (leftIsFile != rightIsFile)
                return leftIsFile ? -1 : 1;

            return byName;
        }

        return left.Length.CompareTo(right.Length);
    }
}
