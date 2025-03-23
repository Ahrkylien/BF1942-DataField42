using System.Diagnostics.CodeAnalysis;
using System.Net;

public class Bf1942Server
{
    public IPAddress Ip { get; init; }
    public int QueryPort { get; init; }
    public Bf1942QueryResult? QueryResult { get; private set; }

    public event VoidEventHandler? NewQuery;

    public Bf1942Server(IPAddress ip, int port)
    {
        Ip = ip;
        QueryPort = port;
    }

    [MemberNotNull(nameof(QueryResult))]
    public async Task QueryServer(TimeSpan? timeoutDuration = null)
    {
        var serverQuery = new Bf1942ServerQuery(Ip, QueryPort);

        // warning because of lacking async support:
        // https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/nullability-improvements/NI-2022-11-01.md
        QueryResult = await serverQuery.Query(timeoutDuration ?? TimeSpan.FromMilliseconds(999));

        NewQuery?.Invoke();
    }
}