using Force.Crc32;
using System.IO;

public static class CheckSums
{
    private static ChecksumRepository _checksumCacheRepository = new("DataFiel42/ChecksumCache.yaml");

    public static uint Crc32(string filePath) => Crc32Algorithm.Compute(File.ReadAllBytes(filePath));
    public static uint Crc32C(string filePath) => Crc32CAlgorithm.Compute(File.ReadAllBytes(filePath));
    public static uint Crc32CWithCache(string filePath)
    {
        var fileSize = new System.IO.FileInfo(filePath).Length;
        var fileLastTimeModified = ((DateTimeOffset)File.GetLastWriteTime(filePath)).ToUnixTimeSeconds();
        (var checksumFound, var checksumString) = _checksumCacheRepository.FindChecksum(fileSize, (ulong)fileLastTimeModified);
        // TODO: safe parsing
        uint checksum;
        if (checksumFound)
            checksum = uint.Parse(checksumString);
        else
        {
            checksum = Crc32C(filePath);
            _checksumCacheRepository.AddRecord(checksum.ToString(), fileSize, (ulong)fileLastTimeModified);
        }
        return checksum;
    }

}