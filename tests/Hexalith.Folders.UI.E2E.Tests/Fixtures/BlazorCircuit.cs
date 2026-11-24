namespace Hexalith.Folders.UI.E2E.Tests.Fixtures;

using System.Text;

using Microsoft.Playwright;

/// <summary>
/// Waits for a Blazor Server circuit to apply its first render, so a test can drive server-handled interactions.
/// The browser acknowledges each applied render batch with an <c>OnRenderCompleted</c> hub call. SignalR sends it over
/// the circuit WebSocket or, when the browser refuses the loopback WebSocket, a long-polling POST.
/// </summary>
public static class BlazorCircuit
{
    // Bounds the wait like Playwright's default action timeout so a broken circuit fails instead of hanging.
    private static readonly TimeSpan FirstRenderTimeout = TimeSpan.FromSeconds(30);

    private static readonly byte[] RenderCompletedMarker = Encoding.UTF8.GetBytes("OnRenderCompleted");

    /// <summary>
    /// Navigates to <paramref name="url"/> and returns once the browser has acknowledged the circuit's first render.
    /// </summary>
    /// <param name="page">The page to navigate. It must be served by an <see cref="InteractiveConsoleHostFixture"/>.</param>
    /// <param name="url">The absolute URL to open.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The navigation response.</returns>
    public static async Task<IResponse?> GotoAndWaitForFirstRenderAsync(
        IPage page,
        string url,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        TaskCompletionSource rendered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnWebSocket(object? sender, IWebSocket socket)
        {
            if (IsCircuitUrl(socket.Url))
            {
                socket.FrameSent += (_, frame) =>
                {
                    if (ContainsRenderCompleted(frame.Binary ?? Encoding.UTF8.GetBytes(frame.Text ?? string.Empty)))
                    {
                        rendered.TrySetResult();
                    }
                };
            }
        }

        void OnRequest(object? sender, IRequest request)
        {
            if (request.Method == "POST"
                && IsCircuitUrl(request.Url)
                && request.PostDataBuffer is { } body
                && ContainsRenderCompleted(body))
            {
                rendered.TrySetResult();
            }
        }

        page.WebSocket += OnWebSocket;
        page.Request += OnRequest;
        try
        {
            IResponse? response = await page.GotoAsync(url).ConfigureAwait(false);
            await rendered.Task.WaitAsync(FirstRenderTimeout, cancellationToken).ConfigureAwait(false);
            return response;
        }
        finally
        {
            page.WebSocket -= OnWebSocket;
            page.Request -= OnRequest;
        }
    }

    private static bool IsCircuitUrl(string url)
        => new Uri(url).AbsolutePath == "/_blazor";

    private static bool ContainsRenderCompleted(byte[] payload)
        => payload.AsSpan().IndexOf(RenderCompletedMarker) >= 0;
}
