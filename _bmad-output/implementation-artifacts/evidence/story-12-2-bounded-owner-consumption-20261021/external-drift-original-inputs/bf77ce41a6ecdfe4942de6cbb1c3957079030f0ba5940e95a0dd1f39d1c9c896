using Bunit;

using Hexalith.Tenants.UI.Components.Pages;
using Hexalith.Tenants.UI.Components.Tenants.Metadata;
using Hexalith.Tenants.UI.State.TenantDetail;

using Microsoft.AspNetCore.Components;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests.Components;

public sealed partial class TenantDetailSurfaceTests
{
    /// <summary>A cancelled detail read owns its token until its refresh finishes.</summary>
    [Theory]
    [InlineData("refresh")]
    [InlineData("route")]
    [InlineData("dispose")]
    public async Task DetailReadCancellationLifetimeExtendsThroughCompletion(string transition)
    {
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        RegisterServices(call =>
        {
            int read = Interlocked.Increment(ref reads);
            if (read == 2)
            {
                CancellationToken token = call.ArgAt<CancellationToken>(2);
                return HoldReadAsync(token);
            }

            return Task.FromResult(Dw150Detail(read == 1 ? "baseline" : "replacement",
                transition == "route" && read > 2 ? "tenant.beta" : "tenant.alpha"));
        });
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        EventCallback refresh = cut.FindComponent<EditTenantMetadataFlow>().Instance.OnProjectionRefreshRequested;
        Task heldRefresh = cut.InvokeAsync(() => refresh.InvokeAsync());

        try
        {
            CancellationToken token = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            token.CanBeCanceled.ShouldBeTrue();
            token.IsCancellationRequested.ShouldBeFalse();

            if (transition == "refresh")
            {
                await cut.InvokeAsync(() => refresh.InvokeAsync()).WaitAsync(TimeSpan.FromSeconds(5));
            }
            else if (transition == "route")
            {
                cut.Render(parameters => parameters.Add(page => page.TenantId, "tenant.beta"));
            }
            else
            {
                await DisposeComponentsAsync();
            }

            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            heldRefresh.IsCompleted.ShouldBeFalse();
            token.IsCancellationRequested.ShouldBeTrue();
            token.WaitHandle.WaitOne(0).ShouldBeTrue();
            bool registeredAfterCancellation = false;
            using (token.Register(() => registeredAfterCancellation = true))
            {
                registeredAfterCancellation.ShouldBeTrue();
            }

            if (transition != "dispose")
            {
                cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-config-read-table']")
                    .TextContent.ShouldContain("replacement"));
            }

            int renderCount = cut.RenderCount;
            release.SetResult(Dw150Detail("superseded"));
            await heldRefresh.WaitAsync(TimeSpan.FromSeconds(5));

            // WaitHandle detects disposed sources even when registration on a cancelled token succeeds.
            Should.Throw<ObjectDisposedException>(() => _ = token.WaitHandle);
            await Renderer.Dispatcher.InvokeAsync(() => { });
            Renderer.UnhandledException.IsCompleted.ShouldBeFalse();
            if (transition == "dispose")
            {
                cut.RenderCount.ShouldBe(renderCount);
                Volatile.Read(ref reads).ShouldBe(2);
            }
            else
            {
                string table = cut.Find("[data-testid='tenants-config-read-table']").TextContent;
                table.ShouldContain("replacement");
                table.ShouldNotContain("superseded");
                cut.Find("[data-testid='tenants-detail-identity']").TextContent
                    .ShouldContain(transition == "route" ? "tenant.beta" : "tenant.alpha");
                Volatile.Read(ref reads).ShouldBe(3);
            }
        }
        finally
        {
            release.TrySetResult(Dw150Detail("superseded"));
            await heldRefresh.WaitAsync(TimeSpan.FromSeconds(5));
        }

        async Task<TenantDetailSnapshot> HoldReadAsync(CancellationToken token)
        {
            using CancellationTokenRegistration registration = token.Register(() => cancelled.TrySetResult());
            entered.SetResult(token);
            return await release.Task;
        }
    }
}
