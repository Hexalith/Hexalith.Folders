using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>Observes real HTTP framing of synthetic fixture traffic; it never starts Dapr or accepts a domain command.</summary>
internal sealed class DurabilityLoopbackHttpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop;

    internal DurabilityLoopbackHttpServer(int requests, Func<int, (HttpStatusCode Status, string Body)> response, CancellationToken token)
    {
        _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        _listener.Start();
        Address = new Uri("http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port);
        Completion = ServeAsync(requests, response);
    }

    internal Uri Address { get; }
    internal Task Completion { get; }
    internal List<(string RequestLine, Dictionary<string, string> Headers, byte[] Body)> Requests { get; } = [];

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        try { await Completion.ConfigureAwait(false); }
        catch (Exception) when (_stop.IsCancellationRequested) { }
        _stop.Dispose();
    }

    private async Task ServeAsync(int count, Func<int, (HttpStatusCode Status, string Body)> response)
    {
        for (int attempt = 1; attempt <= count; attempt++)
        {
            using TcpClient connection = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
            using NetworkStream stream = connection.GetStream();
            string requestLine = await ReadLineAsync(stream, _stop.Token).ConfigureAwait(false);
            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            while (await ReadLineAsync(stream, _stop.Token).ConfigureAwait(false) is { Length: > 0 } line)
            {
                int separator = line.IndexOf(':');
                if (separator < 1) throw new IOException("Invalid loopback HTTP header.");
                headers.Add(line[..separator], line[(separator + 1)..].Trim());
            }

            using MemoryStream body = new();
            if (headers.TryGetValue("Transfer-Encoding", out string? transfer) && transfer.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                while (true)
                {
                    string chunk = await ReadLineAsync(stream, _stop.Token).ConfigureAwait(false);
                    int length = int.Parse(chunk.Split(';', 2)[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    if (length == 0)
                    {
                        while ((await ReadLineAsync(stream, _stop.Token).ConfigureAwait(false)).Length != 0) { }
                        break;
                    }
                    await ReadBodyAsync(stream, body, length, _stop.Token).ConfigureAwait(false);
                    if ((await ReadLineAsync(stream, _stop.Token).ConfigureAwait(false)).Length != 0) throw new IOException("Invalid loopback HTTP chunk.");
                }
            }
            else if (headers.TryGetValue("Content-Length", out string? length))
            {
                await ReadBodyAsync(stream, body, int.Parse(length, CultureInfo.InvariantCulture), _stop.Token).ConfigureAwait(false);
            }

            Requests.Add((requestLine, headers, body.ToArray()));
            (HttpStatusCode status, string text) = response(attempt);
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            byte[] head = Encoding.ASCII.GetBytes($"HTTP/1.1 {(int)status} Test\r\nContent-Length: {bytes.Length}\r\nContent-Type: application/problem+json\r\nRetry-After: 0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(head, _stop.Token).ConfigureAwait(false);
            await stream.WriteAsync(bytes, _stop.Token).ConfigureAwait(false);
        }
    }

    private static async Task ReadBodyAsync(Stream stream, MemoryStream destination, int length, CancellationToken token)
    {
        if (length < 0 || destination.Length + length > 1_048_576) throw new IOException("Loopback test body exceeded its bound.");
        byte[] bytes = new byte[length];
        await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        await destination.WriteAsync(bytes, token).ConfigureAwait(false);
    }

    private static async Task<string> ReadLineAsync(Stream stream, CancellationToken token)
    {
        using MemoryStream line = new();
        byte[] single = new byte[1];
        while (line.Length < 65_536)
        {
            await stream.ReadExactlyAsync(single, token).ConfigureAwait(false);
            if (single[0] == '\n') return Encoding.ASCII.GetString(line.ToArray()).TrimEnd('\r');
            line.WriteByte(single[0]);
        }
        throw new IOException("Loopback test header exceeded its bound.");
    }
}
