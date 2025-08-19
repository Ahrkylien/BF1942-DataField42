using System.Diagnostics.CodeAnalysis;
using System.Net;

public record Bf1942Server(IPAddress Ip, int QueryPort)
{
    public Bf1942QueryResult? QueryResult { get; private set; }

    public event VoidEventHandler? NewQuery;

    [MemberNotNull(nameof(QueryResult))]
    public async Task QueryServer(TimeSpan? timeoutDuration = null)
    {
        var serverQuery = new Bf1942ServerQuery(Ip, QueryPort);

        // warning because of lacking async support:
        // https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/nullability-improvements/NI-2022-11-01.md
        QueryResult = await serverQuery.Query(timeoutDuration ?? TimeSpan.FromMilliseconds(999));

        NewQuery?.Invoke();
    }

    public virtual bool Equals(Bf1942Server? other)
    {
        if (other is null)
            return false;

        return other.Ip.Equals(Ip) && other.QueryPort == QueryPort;
    }

    public override int GetHashCode() => HashCode.Combine(Ip, QueryPort);
}