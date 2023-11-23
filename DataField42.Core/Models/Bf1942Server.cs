public class Bf1942Server
{
    public string Ip { get; init; }
    public int QueryPort { get; init; }
    public Bf1942QueryResult? QueryResult { get; private set; }

    public Bf1942Server(string ip, int port)
    {
        Ip = ip;
        QueryPort = port;
    }

    public void QueryServer()
    {
        var serverQuery = new Bf1942ServerQuery(Ip, QueryPort);
        QueryResult = serverQuery.Query();
    }
}

