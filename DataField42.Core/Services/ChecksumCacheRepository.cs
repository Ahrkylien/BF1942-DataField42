using YamlDotNet.Serialization;
using System.IO;

public class ChecksumRepository
{
    private readonly string _filename;
    private readonly List<ChecksumRecord> _records;
    private readonly object lockObj = new();
    private readonly IDeserializer deserializer = new DeserializerBuilder().Build();
    private readonly ISerializer serializer = new SerializerBuilder().Build();

    public ChecksumRepository(string filename)
    {
        _filename = filename;
        _records = LoadRecords();
    }

    private List<ChecksumRecord> LoadRecords()
    {
        try
        {
            string yaml = File.ReadAllText(_filename);
            return deserializer.Deserialize<List<ChecksumRecord>>(yaml);
        }
        catch
        {
            return new List<ChecksumRecord>();
        }
    }

    private void SaveRecords()
    {
        FileHelper.WriteText(_filename, serializer.Serialize(_records));
    }

    public void AddRecord(string checksum, long size, ulong lastTimeModified)
    {
        lock (lockObj)
        {
            _records.Add(new ChecksumRecord(checksum, size, lastTimeModified));
            SaveRecords();
        }
    }

    public (bool, string) FindChecksum(long size, ulong lastTimeModified)
    {
        lock (lockObj)
        {
            var checksum = _records.FirstOrDefault(x => x.Size == size && x.LastTimeModified == lastTimeModified).Checksum ?? "";
            return (checksum != "", checksum);
        }
    }

    private struct ChecksumRecord
    {
        public string Checksum { get; set; }
        public long Size { get; set; }
        public ulong LastTimeModified { get; set; }

        public ChecksumRecord(string checksum, long size, ulong lastTimeModified)
        {
            Checksum = checksum;
            Size = size;
            LastTimeModified = lastTimeModified;
        }
    }
}

