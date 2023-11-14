public static class IntExtension
{
    public static string ToReadableFileSize(this int size)
    {
        decimal sizeDecimal = size;
        string[] sizes = { "bytes", "KB", "MB", "GB", "TB" };
        int order = 0;
        while (sizeDecimal >= 1024 && order < sizes.Length - 1)
        {
            order++;
            sizeDecimal /= 1024;
        }
        var numberOfDigitsBeforeDot = ((int)sizeDecimal).ToString().Length;
        var numberOfDecimals = Math.Max(3 - numberOfDigitsBeforeDot, 0);
        return string.Format($"{{0:F{numberOfDecimals}}} {{1}}", sizeDecimal, sizes[order]);
    }
}

public static class UlongExtension
{
    public static string ToReadableFileSize(this ulong size)
    {
        decimal sizeDecimal = size;
        string[] sizes = { "bytes", "KB", "MB", "GB", "TB" };
        int order = 0;
        while (sizeDecimal >= 1024 && order < sizes.Length - 1)
        {
            order++;
            sizeDecimal /= 1024;
        }
        var numberOfDigitsBeforeDot = ((int)sizeDecimal).ToString().Length;
        var numberOfDecimals = Math.Max(3 - numberOfDigitsBeforeDot, 0);
        return string.Format($"{{0:F{numberOfDecimals}}} {{1}}", sizeDecimal, sizes[order]);
    }   
}

public static class IEnumerableExtension
{
    public static ulong Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, ulong> summer)
    {
        ulong total = 0;

        foreach (var item in source)
            total += summer(item);

        return total;
    }
}