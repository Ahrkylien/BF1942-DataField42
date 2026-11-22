using System.Text.Json;
using DataField42.Website.Data;
using DataField42.Website.Data.Entities;

namespace DataField42.Website.Services.ClientServer;

/// <summary>
/// A client asking for the files it needs to play a map: <c>download map mod ip port keyhash</c>.
/// </summary>
/// <remarks>
/// The client is first told which files there are, and says for each whether it wants it, since it
/// keeps what it already has. It is then sent the ones it wants, each announced, sent and
/// acknowledged. Every request is recorded in the request log, with how it ended.
/// </remarks>
public sealed class DownloadHandler(
    ClientFileSelector selector,
    DataField42DbContext context,
    ILogger<DownloadHandler> logger)
{
    private const int RequestLogType = 1;

    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromMinutes(15);

    /// <param name="connection">The client.</param>
    /// <param name="clientIp">The client's address.</param>
    /// <param name="arguments">The request after its name: map, mod, the game server's address and port, the player's key hash, and optional key and value pairs.</param>
    public async Task HandleAsync(DataField42MessageStream messages, string clientIp, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        string map = arguments[0], mod = arguments[1], serverAddress = $"{arguments[2]}:{arguments[3]}", keyHash = arguments[4];
        string result = "Success";

        try
        {
            logger.LogInformation("{Ip} wants {Map} of {Mod} for {Server}", clientIp, map, mod, serverAddress);
            await SendFilesAsync(messages, await selector.SelectAsync(map, mod, cancellationToken), cancellationToken);
        }
        catch (Exception ex)
        {
            result = ex.Message;
            throw;
        }
        finally
        {
            await LogRequestAsync(clientIp, map, mod, serverAddress, keyHash, arguments.Skip(5), result);
        }
    }

    private static async Task SendFilesAsync(DataField42MessageStream messages, List<ClientFile> files, CancellationToken cancellationToken)
    {
        await messages.SendMessageAsync(string.Join('\n', files.Select(file => file.Info.ToProtocolString())), cancellationToken);

        // The answer is "yes" or "no" for each file, so it is no longer than that. The client may take
        // its time deciding.
        var answers = (await messages.ReceiveMessage(AnswerTimeout, cancellationToken, maxLength: 4 * files.Count))
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (answers.Length != files.Count)
        {
            await messages.SendMessageAsync("no 0 0", cancellationToken);
            throw new ClientRequestException($"file info length response incorrect: {answers.Length} != {files.Count}");
        }

        var wanted = files.Where((_, index) => answers[index] == "yes").ToList();
        await messages.SendMessageAndAwaitAcknowledgementAsync($"yes {wanted.Count} {wanted.Sum(file => (long)file.Info.Size)}", cancellationToken);

        foreach (var file in wanted)
        {
            await messages.SendMessageAndAwaitAcknowledgementAsync(file.Info.ToProtocolString(), cancellationToken);
            await messages.SendFileAsync(file.FullPath, (long)file.Info.Size, cancellationToken);
            await messages.AwaitAcknowledgement(cancellationToken: cancellationToken);
        }

        await messages.AwaitAcknowledgement(cancellationToken: cancellationToken);
    }

    private async Task LogRequestAsync(string clientIp, string map, string mod, string server, string keyHash, IEnumerable<string> extra, string result)
    {
        try
        {
            context.RequestLogs.Add(new RequestLog
            {
                Type = RequestLogType,
                Date = DateTime.Now,
                Ip = Fit(clientIp, 255),
                Field1 = Fit(map, 255),
                Field2 = Fit(mod, 255),
                Field3 = Fit(server, 255),
                Field4 = Fit(keyHash, 2048),
                Field5 = Fit(JsonSerializer.Serialize(extra), 255),
                Field6 = Fit(result, 255),
            });
            await context.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // A request that was served is not undone because it could not be written down.
            logger.LogWarning(ex, "Could not log the request of {Ip}", clientIp);
        }
    }

    private static string Fit(string text, int length) => text.Length <= length ? text : text[..length];
}
