using System.Net.Sockets;
using System.Net;
using System.Text;

public class Bf1942ServerQuery
{
    private readonly IPAddress _ip;
    private readonly int _port;

    public Bf1942ServerQuery(IPAddress ip, int port)
    {
        _ip = ip;
        _port = port;
    }

    public async Task<Bf1942QueryResult> Query(TimeSpan timeoutDuration)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(timeoutDuration);

        Dictionary<string, string> properties = new();
        UdpClient udpClient = new();

        try
        {
            udpClient.Connect(_ip, _port);
            await udpClient.SendAsync(Encoding.UTF8.GetBytes("\\status\\"), cancellationTokenSource.Token);

            bool receivedFinalPacket = false;
            uint expectedNumberOfPackets = 0;

            for (int i = 0; i < 20; i++)
            {
                byte[] receiveBytes = (await udpClient.ReceiveAsync(cancellationTokenSource.Token)).Buffer;

                string dataString = Bf1942Encoding.Decode(receiveBytes);
                var dataList = dataString.Split("\\");

                for (int j = 0; j < dataList.Length / 2; j++)
                    properties[dataList[j * 2 + 1]] = dataList[j * 2 + 2];

                if (properties.ContainsKey("final"))
                {
                    receivedFinalPacket = true;
                    expectedNumberOfPackets = uint.Parse(properties["queryid"].Split('.')[1]);
                }

                if (receivedFinalPacket && i + 1 == expectedNumberOfPackets)
                    break;
            }
        }
        catch (OperationCanceledException ex)
        {
            throw new TimeoutException($"Querying server {_ip}:{_port} timed out", ex);
        }
        finally
        {
            udpClient.Close();
        }

        return new Bf1942QueryResult(properties);
    }
}
