using System.Net;
using System.Net.Sockets;

namespace DataField42.Website.Services.ClientServer;

/// <summary>
/// Listens for the DataField42 clients and servers, which connect on a port of their own and not
/// through the web server.
/// </summary>
/// <remarks>
/// Anyone can connect, so there is a limit to how many connections are served at once, and to how
/// many of them are from one address. An address may be many players behind one router, hence the
/// room.
/// </remarks>
public sealed class ClientServerHost(IServiceScopeFactory scopeFactory, ReleaseFiles releases, ILogger<ClientServerHost> logger) : BackgroundService
{
    private const int Port = DataField42Communication.DefaultPort;
    private const int MaxConnections = 500;
    private const int MaxConnectionsPerAddress = 20;

    /// <summary>
    /// How long a client may be silent, or a write may stall, unless a request allows it longer.
    /// </summary>
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(1);

    private readonly Lock _lock = new();
    private readonly Dictionary<IPAddress, int> _connectionsPerAddress = [];
    private int _connections;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new TcpListener(IPAddress.Any, Port);

        try
        {
            listener.Start();
        }
        catch (SocketException ex)
        {
            // Another instance may have the port. The website is no worse for it.
            logger.LogError(ex, "Clients cannot connect: port {Port} could not be opened", Port);
            return;
        }

        logger.LogInformation("Clients can connect on port {Port}; the server script they are offered is version {Version}", Port, releases.ServerVersion);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stoppingToken);
                _ = Task.Run(() => Serve(client, stoppingToken), CancellationToken.None);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task Serve(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            if ((client.Client.RemoteEndPoint as IPEndPoint)?.Address is not { } address)
                return;

            string ip = address.ToString();

            if (!TryEnter(address))
            {
                logger.LogDebug("{Ip}: turned away, there are too many connections", ip);
                return;
            }

            try
            {
                client.NoDelay = true;
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ClientRequestHandler>()
                    .Handle(new DataField42MessageStream(client.GetStream(), IdleTimeout), ip, cancellationToken);
            }
            catch (Exception ex) when (ex is ClientRequestException or TimeoutException or IOException or InvalidDataException)
            {
                // The client went away, asked for something that is not there, or fell silent.
                logger.LogInformation("{Ip}: {Message}", ip, ex.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "{Ip}: a request failed", ip);
            }
            finally
            {
                Leave(address);
            }
        }
    }

    private bool TryEnter(IPAddress address)
    {
        using (_lock.EnterScope())
        {
            _connectionsPerAddress.TryGetValue(address, out int fromAddress);
            if (_connections >= MaxConnections || fromAddress >= MaxConnectionsPerAddress)
                return false;

            _connectionsPerAddress[address] = fromAddress + 1;
            _connections++;
            return true;
        }
    }

    private void Leave(IPAddress address)
    {
        using (_lock.EnterScope())
        {
            _connections--;
            if (--_connectionsPerAddress[address] == 0)
                _connectionsPerAddress.Remove(address);
        }
    }
}
