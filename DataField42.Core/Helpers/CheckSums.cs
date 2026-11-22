using Force.Crc32;

public static class CheckSums
{
    private const int ReadBufferSize = 1 << 20;

    private static readonly ChecksumRepository _checksumCacheRepository = new("DataField42/ChecksumCache.tsv");

    public static uint Crc32(string filePath) => Crc32Algorithm.Compute(File.ReadAllBytes(filePath));

    /// <summary>
    /// The CRC-32C of a file, read in chunks rather than whole: the game's archives are hundreds of
    /// megabytes.
    /// </summary>
    public static uint Crc32C(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, ReadBufferSize, FileOptions.SequentialScan);
        var buffer = new byte[ReadBufferSize];

        uint crc = 0;
        int read;
        while ((read = stream.Read(buffer)) > 0)
            crc = Crc32CAlgorithm.Append(crc, buffer, 0, read);

        return crc;
    }

    public static async Task<uint> Crc32CAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, ReadBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[ReadBufferSize];

        uint crc = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            crc = Crc32CAlgorithm.Append(crc, buffer, 0, read);

        return crc;
    }

    public static uint Crc32CWithCache(string filePath) => Convert.ToUInt32(_checksumCacheRepository.Get(filePath), 16);
}
