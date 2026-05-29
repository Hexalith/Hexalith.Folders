using Microsoft.AspNetCore.Components;

namespace Hexalith.Folders.UI.Components;

/// <summary>
/// Story 6.10 / F-7 (architecture.md L551) / §3.7 (ops-console-wireflows §3.7) / UX-DR25 — the timed
/// perceived-wait component. While a page read is in flight the page renders this in its loading branch; the
/// component re-renders itself at 400 ms (show the layout-stable skeleton) and at 2 s (show the
/// "still loading… [Cancel]" affordance) without the page restructuring its data <c>await</c>. Both thresholds
/// are scheduled off the injected BCL <see cref="TimeProvider"/> (registered as <c>TimeProvider.System</c>,
/// no package added) so they are deterministically testable via a hand-rolled controllable provider; both
/// timers are one-shot, measured absolutely from mount, and disposed with the component (no leaked circuit
/// timers).
/// </summary>
public partial class SkeletonState : ComponentBase, IDisposable
{
    /// <summary>The skeleton appears after the request has been in flight this long (F-7 / §3.7).</summary>
    private static readonly TimeSpan SkeletonDelay = TimeSpan.FromMilliseconds(400);

    /// <summary>The "still loading… [Cancel]" affordance appears after this long, measured from mount (F-7).</summary>
    private static readonly TimeSpan CancelDelay = TimeSpan.FromSeconds(2);

    private ITimer? _skeletonTimer;
    private ITimer? _cancelTimer;
    private bool _showSkeleton;
    private bool _showCancel;

    /// <summary>Gets or sets the human label naming what is loading (UX-DR25), e.g. "search results".</summary>
    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = default!;

    /// <summary>
    /// Gets or sets the page's <c>console-page-{name}-loading</c> token, rendered on the root region so the
    /// existing loading selectors/tests keep resolving after the simple paragraph is replaced (AC #6).
    /// </summary>
    [Parameter]
    public string? TestId { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the operator activates the 2 s Cancel control. The page wires
    /// this to its per-load <c>CancellationTokenSource.Cancel()</c> (a read-query cancel only).
    /// </summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    /// <summary>Gets or sets an optional extra CSS class.</summary>
    [Parameter]
    public string? AdditionalCssClass { get; set; }

    /// <summary>The injected BCL clock (<c>TimeProvider.System</c> in production; controllable in tests).</summary>
    [Inject]
    private TimeProvider Clock { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // Two one-shot timers, both measured from mount. The callback marshals back onto the Blazor circuit
        // via InvokeAsync (timers fire on a pool thread); the returned Task is intentionally not awaited — the
        // re-render is the only effect.
        _skeletonTimer = Clock.CreateTimer(
            _ => InvokeAsync(() =>
            {
                _showSkeleton = true;
                StateHasChanged();
            }),
            null,
            SkeletonDelay,
            Timeout.InfiniteTimeSpan);

        _cancelTimer = Clock.CreateTimer(
            _ => InvokeAsync(() =>
            {
                _showCancel = true;
                StateHasChanged();
            }),
            null,
            CancelDelay,
            Timeout.InfiniteTimeSpan);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Dispose both timers so a teardown mid-wait never fires a callback into a torn-down circuit.
        _skeletonTimer?.Dispose();
        _cancelTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
