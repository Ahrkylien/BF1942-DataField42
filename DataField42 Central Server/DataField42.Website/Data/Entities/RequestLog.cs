namespace DataField42.Website.Data.Entities;

/// <summary>
/// A request the central server handled, kept for diagnostics. The meaning of the fields depends
/// on <see cref="Type"/>.
/// </summary>
public class RequestLog
{
    public int Id { get; set; }

    public int? Type { get; set; }

    public DateTime? Date { get; set; }

    public string? Ip { get; set; }

    public string? Field1 { get; set; }

    public string? Field2 { get; set; }

    public string? Field3 { get; set; }

    public string? Field4 { get; set; }

    public string? Field5 { get; set; }

    public string? Field6 { get; set; }
}
