using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Xunit;

namespace Hexalith.Folders.AppHost.Tests;

/// <summary>
/// Reusable Tier-3 Aspire harness that boots the full Folders <c>AppHost</c> topology (EventStore gateway +
/// actor host, Tenants, Folders server/workers/UI, and the Memories search-index server with its Redis Stack /
/// FalkorDB containers) through <see cref="DistributedApplicationTestingBuilder"/> and waits for every
/// publish/subscribe participant to reach <see cref="KnownResourceStates.Running"/>.
/// </summary>
/// <remarks>
/// <para>
/// Booting the topology requires a DCP-capable host (Docker running plus a Dapr runtime, e.g. <c>dapr init</c>).
/// The hermetic CI lanes do not provide that, and the wider environment has a known Aspire CLI/DCP boot mismatch
/// (Epic 9 residual), so this fixture is <strong>opt-in</strong>: it only boots when
/// <see cref="OptInEnvironmentVariable"/> is set to <c>true</c>/<c>1</c>. Otherwise <see cref="IsAvailable"/>
/// stays <see langword="false"/> and every test calls <see cref="SkipIfUnavailable"/> to skip cleanly, keeping
/// the full-solution test lane green without DCP. A dedicated DCP-capable lane sets the variable to run them.
/// </para>
/// <para>
/// This proves process-level cross-process wiring (the dormant Epic 9 routing actually activates and the six
/// services come up together with the Story 10.3 D1 folders.events override + production pub/sub scopes). The
/// deeper folder-mutation → worker-receipt assertion layers on top of this same harness; it is currently moot
/// because the production content materializer is fail-closed (Story 10.3 Task 4) until a real workspace reader
/// is wired.
/// </para>
/// </remarks>
public sealed class AspireFoldersAppHostFixture : IAsyncLifetime
{
    /// <summary>The opt-in environment variable that enables the DCP-backed boot.</summary>
    public const string OptInEnvironmentVariable = "HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION";

    /// <summary>
    /// The Folders AppHost project resources spanning the cross-process semantic-indexing publish/subscribe path:
    /// the <c>eventstore</c> folder-events publisher, the <c>folders-workers</c>
    /// (<c>/folders/events</c> + <c>memories-events</c>) subscriber, the <c>memories</c> (<c>memories-events</c>)
    /// subscriber, and the <c>folders</c>/<c>tenants</c>/<c>folders-ui</c> services.
    /// </summary>
    public static readonly IReadOnlyList<string> TopologyResourceNames =
    [
        "eventstore",
        "tenants",
        "folders",
        "folders-workers",
        "folders-ui",
        "memories",
    ];

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(8);

    private readonly Dictionary<string, string?> _environmentSnapshot = new(StringComparer.Ordinal);
    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;

    /// <summary>Gets a value indicating whether the topology was opted into and booted.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Gets the reason a test is skipped when <see cref="IsAvailable"/> is <see langword="false"/>.</summary>
    public string SkipReason { get; private set; } = string.Empty;

    /// <summary>Gets the running distributed application; throws when the topology was not booted.</summary>
    public DistributedApplication App => _app ?? throw new InvalidOperationException(
        "The Aspire topology was not started. Call SkipIfUnavailable() first; the fixture only boots when " +
        $"{OptInEnvironmentVariable}=true on a DCP-capable host.");

    /// <summary>Skips the current test when the topology was not opted into / could not boot.</summary>
    public void SkipIfUnavailable()
    {
        if (!IsAvailable)
        {
            Assert.Skip(SkipReason);
        }
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        if (!OptInEnabled())
        {
            SkipReason =
                $"Set {OptInEnvironmentVariable}=true on a DCP-capable host (Docker running + 'dapr init') to run " +
                "the Folders Aspire cross-process integration tests. They boot the full AppHost topology and are " +
                "intentionally excluded from the hermetic lanes.";
            return;
        }

        // Boot in Development with Keycloak disabled so the topology starts without an external identity provider
        // (the AppHost omits the Keycloak resource and JWT wiring when EnableKeycloak=false). Snapshot/restore so
        // these process-wide mutations do not leak into other test collections.
        SnapshotAndSet("EnableKeycloak", "false");
        SnapshotAndSet("ASPNETCORE_ENVIRONMENT", "Development");
        SnapshotAndSet("DOTNET_ENVIRONMENT", "Development");

        using CancellationTokenSource startupCts = new(StartupTimeout);
        try
        {
            _builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_Folders_AppHost>([], startupCts.Token)
                .ConfigureAwait(false);

            _app = await _builder.BuildAsync(startupCts.Token).ConfigureAwait(false);
            await _app.StartAsync(startupCts.Token).ConfigureAwait(false);

            foreach (string resourceName in TopologyResourceNames)
            {
                await _app.ResourceNotifications
                    .WaitForResourceAsync(resourceName, KnownResourceStates.Running, startupCts.Token)
                    .ConfigureAwait(false);
            }

            IsAvailable = true;
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync().ConfigureAwait(false);
            _app = null;
        }

        if (_builder is not null)
        {
            await _builder.DisposeAsync().ConfigureAwait(false);
            _builder = null;
        }

        RestoreEnvironmentSnapshot();
    }

    private static bool OptInEnabled()
    {
        string? value = Environment.GetEnvironmentVariable(OptInEnvironmentVariable);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.Ordinal);
    }

    private void SnapshotAndSet(string name, string? value)
    {
        if (!_environmentSnapshot.ContainsKey(name))
        {
            _environmentSnapshot[name] = Environment.GetEnvironmentVariable(name);
        }

        Environment.SetEnvironmentVariable(name, value);
    }

    private void RestoreEnvironmentSnapshot()
    {
        foreach (KeyValuePair<string, string?> entry in _environmentSnapshot)
        {
            Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        }

        _environmentSnapshot.Clear();
    }
}
