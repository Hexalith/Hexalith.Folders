using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Resources;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Hexalith.EventStore.Client.Queries;
using Hexalith.FrontComposer.Contracts.Registration;
using Hexalith.FrontComposer.Shell.Components.Icons;
using Hexalith.FrontComposer.Shell.Extensions;
using Hexalith.FrontComposer.Shell.Services.Auth;
using Hexalith.Memories.Client.Rest;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Queries;
using Hexalith.Tenants.UI.Components.Pages;
using Hexalith.Tenants.UI.Composition;
using Hexalith.Tenants.UI.Extensions;
using Hexalith.Tenants.UI.Resources;
using Hexalith.Tenants.UI.Services;
using Hexalith.Tenants.UI.Services.Configuration;
using Hexalith.Tenants.UI.Services.Gateways;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantDetail;
using Hexalith.Tenants.UI.State.TenantList;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.FluentUI.AspNetCore.Components;

using NSubstitute;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests;

public sealed class TenantsUiCompositionTests
{
    private static readonly TimeSpan DependencyResolutionTimeout = TimeSpan.FromMinutes(3);

    [Fact]
    public void FrontComposer_registration_exposes_tenants_nav_entries_and_generated_surface_manifest()
    {
        CapturingRegistry registry = new();

        TenantsFrontComposerRegistration.RegisterDomain(registry);

        // Domain menu is contributed as declarative data; the shell renders exactly one Tenants module entry
        // while page-local tabs own the Tenants-domain sub-surfaces.
        FrontComposerNavEntry navEntry = registry.NavEntries.ShouldHaveSingleItem();
        navEntry.Title.ShouldBe("Tenants");
        navEntry.Href.ShouldBe("/tenants");
        navEntry.BoundedContext.ShouldBe("tenants");
        navEntry.RequiredPolicy.ShouldBeNull();
        navEntry.TitleKey.ShouldBe("Tenants.Navigation.Tenants");
        navEntry.Resource.ShouldBe(typeof(TenantsResources));
        navEntry.Order.ShouldBe(0);

        // The legacy AddNavGroup stub is no longer used.
        registry.NavGroups.ShouldBeEmpty();

        DomainManifest manifest = registry.Manifests.ShouldHaveSingleItem();
        manifest.BoundedContext.ShouldBe("tenants");
        manifest.Projections.ShouldBe([typeof(TenantSummaryProjection).FullName!]);
        manifest.Commands.ShouldBe([typeof(CreateTenantCommand).FullName!]);
        manifest.FullPageCommands.ShouldBeEmpty();
        manifest.Icon.ShouldBe("Regular.Size20.BuildingPeople");
        manifest.NameKey.ShouldBe("Tenants.Navigation.Tenants");
        manifest.Resource.ShouldBe(typeof(TenantsResources));

        FcFluentIcons.TryCreate(manifest.Icon, out Icon? tenantIcon).ShouldBeTrue();
        tenantIcon.ShouldNotBeNull();
        tenantIcon!.Name.ShouldBe("BuildingPeople");
        tenantIcon.Size.ShouldBe(IconSize.Size20);
        tenantIcon.Variant.ShouldBe(IconVariant.Regular);
    }

    [Fact]
    public void Bff_composition_marks_read_and_command_surfaces_connected_after_command_gateway_story()
    {
        ITenantsBffComposition composition = new TenantsBffComposition(
            new StubTenantCommandGateway(),
            readSurface: new TenantsReadSurfaceAvailability(IsConnected: true));

        composition.IsReadSurfaceConnected.ShouldBeTrue();
        composition.IsCommandSurfaceConnected.ShouldBeTrue();
    }

    /// <summary>
    /// An unregistered read surface must read as disconnected. It previously reported connected, so any
    /// composition that did not supply the dependency claimed a working read surface with no evidence —
    /// and that flag gates notification leases and the grant read-surface guard.
    /// </summary>
    [Fact]
    public void Bff_composition_read_surface_fails_closed_when_availability_is_not_composed()
    {
        ITenantsBffComposition composition = new TenantsBffComposition(new StubTenantCommandGateway());

        composition.IsReadSurfaceConnected.ShouldBeFalse();
    }

    [Fact]
    public void Bff_composition_read_surface_is_disconnected_when_no_tenants_base_address_was_configured()
    {
        ITenantsBffComposition composition = new TenantsBffComposition(
            new StubTenantCommandGateway(),
            readSurface: new TenantsReadSurfaceAvailability(IsConnected: false));

        composition.IsReadSurfaceConnected.ShouldBeFalse();
    }

    [Fact]
    public void Bff_composition_keeps_command_surface_disconnected_for_unavailable_gateway()
    {
        ITenantsBffComposition composition = new TenantsBffComposition(
            new UnavailableTenantCommandGateway(),
            readSurface: new TenantsReadSurfaceAvailability(IsConnected: true));

        composition.IsReadSurfaceConnected.ShouldBeTrue();
        composition.IsCommandSurfaceConnected.ShouldBeFalse();
    }

    [Fact]
    public void Aggregate_command_admission_gate_is_scoped_to_a_circuit()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        ServiceDescriptor descriptor = services.Single(static candidate =>
            candidate.ServiceType == typeof(TenantAggregateCommandAdmissionGate));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope firstScope = provider.CreateScope();
        using IServiceScope secondScope = provider.CreateScope();
        TenantAggregateCommandAdmissionGate first = firstScope.ServiceProvider
            .GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantAggregateCommandAdmissionGate firstAgain = firstScope.ServiceProvider
            .GetRequiredService<TenantAggregateCommandAdmissionGate>();
        TenantAggregateCommandAdmissionGate second = secondScope.ServiceProvider
            .GetRequiredService<TenantAggregateCommandAdmissionGate>();

        firstAgain.ShouldBeSameAs(first);
        second.ShouldNotBeSameAs(first);
    }

    [Fact]
    public void Host_supplied_attempt_store_is_shared_by_create_and_other_tenant_flows()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        TenantAggregateCommandAdmissionGate gate = new();
        TenantCommandAttemptTracker supplied = new(gate);
        services.AddScoped(_ => gate);
        services.AddScoped(_ => supplied);
        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantCommandAttemptTracker>().ShouldBeSameAs(supplied);
        scope.ServiceProvider.GetRequiredService<TenantCreateAttemptTracker>().Attempts.ShouldBeSameAs(supplied);
    }

    [Fact]
    public void Lifecycle_attempt_tracker_is_scoped_to_a_circuit()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        ServiceDescriptor descriptor = services.Single(static candidate =>
            candidate.ServiceType == typeof(TenantLifecycleAttemptTracker));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope firstScope = provider.CreateScope();
        using IServiceScope secondScope = provider.CreateScope();
        TenantLifecycleAttemptTracker first = firstScope.ServiceProvider
            .GetRequiredService<TenantLifecycleAttemptTracker>();
        TenantLifecycleAttemptTracker firstAgain = firstScope.ServiceProvider
            .GetRequiredService<TenantLifecycleAttemptTracker>();
        TenantLifecycleAttemptTracker second = secondScope.ServiceProvider
            .GetRequiredService<TenantLifecycleAttemptTracker>();

        firstAgain.ShouldBeSameAs(first);
        second.ShouldNotBeSameAs(first);
    }

    [Fact]
    public void Correction_attempt_tracker_is_scoped_to_a_circuit()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        ServiceDescriptor descriptor = services.Single(static candidate =>
            candidate.ServiceType == typeof(TenantCorrectionAttemptTracker));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope firstScope = provider.CreateScope();
        using IServiceScope secondScope = provider.CreateScope();
        TenantCorrectionAttemptTracker first = firstScope.ServiceProvider.GetRequiredService<TenantCorrectionAttemptTracker>();
        firstScope.ServiceProvider.GetRequiredService<TenantCorrectionAttemptTracker>().ShouldBeSameAs(first);
        secondScope.ServiceProvider.GetRequiredService<TenantCorrectionAttemptTracker>().ShouldNotBeSameAs(first);
    }

    [Fact]
    public void Create_audit_return_state_is_scoped_to_a_circuit()
    {
        // Component tests construct or register this state themselves, so only this pin catches a missing
        // registration (restoration silently disabled) or a singleton (one caller's create state in another circuit).
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        ServiceDescriptor descriptor = services.Single(static candidate =>
            candidate.ServiceType == typeof(TenantCreateAuditReturnState));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope firstScope = provider.CreateScope();
        using IServiceScope secondScope = provider.CreateScope();
        TenantCreateAuditReturnState first = firstScope.ServiceProvider
            .GetRequiredService<TenantCreateAuditReturnState>();
        TenantCreateAuditReturnState firstAgain = firstScope.ServiceProvider
            .GetRequiredService<TenantCreateAuditReturnState>();
        TenantCreateAuditReturnState second = secondScope.ServiceProvider
            .GetRequiredService<TenantCreateAuditReturnState>();

        firstAgain.ShouldBeSameAs(first);
        second.ShouldNotBeSameAs(first);
    }

    [Fact]
    public void Remove_configuration_attempt_tracker_is_scoped_to_a_circuit()
    {
        // Nothing else pins this registration: every remove component test supplies its own tracker, so a
        // missing descriptor would only surface in production as a permanently blocked tracked dispatch,
        // and a singleton would share one tenant's retained attempt and lease owner across every circuit.
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        ServiceDescriptor descriptor = services.Single(static candidate =>
            candidate.ServiceType == typeof(TenantRemoveConfigurationAttemptTracker));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope firstScope = provider.CreateScope();
        using IServiceScope secondScope = provider.CreateScope();
        TenantRemoveConfigurationAttemptTracker first = firstScope.ServiceProvider
            .GetRequiredService<TenantRemoveConfigurationAttemptTracker>();
        TenantRemoveConfigurationAttemptTracker firstAgain = firstScope.ServiceProvider
            .GetRequiredService<TenantRemoveConfigurationAttemptTracker>();
        TenantRemoveConfigurationAttemptTracker second = secondScope.ServiceProvider
            .GetRequiredService<TenantRemoveConfigurationAttemptTracker>();

        firstAgain.ShouldBeSameAs(first);
        second.ShouldNotBeSameAs(first);
    }

    /// <summary>
    /// Resolves the composed graph rather than inspecting descriptors, so a container cycle is caught.
    /// </summary>
    /// <remarks>
    /// TenantsBffComposition and TenantQueryGateway each took the other as an optional constructor
    /// parameter. Optional parameters are still resolved when the service is registered, so the container
    /// threw "A circular dependency was detected" as soon as Tenants:BaseAddress was configured — that is,
    /// exactly when the read transport was switched on. Every existing composition test asserted on
    /// ServiceDescriptors or resolved only ITenantsRestQueryClient, so none of them constructed the graph
    /// and the cycle was invisible.
    /// </remarks>
    [Fact]
    public async Task Composed_read_graph_resolves_without_a_dependency_cycle()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:BaseAddress"] = "https://tenants.invalid",
                ["EventStore:BaseAddress"] = "https://eventstore.invalid",
                ["Tenants:Audit:PermissionRecoveryHref"] = "/support/audit-access",
                ["Tenants:Audit:EscalationRecoveryHref"] = "/support/audit-incident",
            })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        ITenantsBffComposition composition = scope.ServiceProvider.GetRequiredService<ITenantsBffComposition>();
        ITenantQueryGateway gateway = scope.ServiceProvider.GetRequiredService<ITenantQueryGateway>();
        TenantReadRefreshSubscription refreshSubscription = scope.ServiceProvider
            .GetRequiredService<TenantReadRefreshSubscription>();

        composition.ShouldNotBeNull();
        gateway.ShouldNotBeNull();
        refreshSubscription.ShouldNotBeNull();
        composition.IsReadSurfaceConnected.ShouldBeTrue();
        composition.AuditPermissionRecoveryHref.ShouldBe("/support/audit-access");
        composition.AuditEscalationRecoveryHref.ShouldBe("/support/audit-incident");
    }

    [Fact]
    public void Host_query_gateway_override_requires_and_uses_matching_availability_contract()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        ITenantQueryGateway overrideGateway = Substitute.For<ITenantQueryGateway>();
        services.AddSingleton(configuration);
        services.AddSingleton(overrideGateway);
        services.AddSingleton<ITenantsReadSurfaceAvailability>(
            new TenantsReadSurfaceAvailability(IsConnected: true));

        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantQueryGateway>().ShouldBeSameAs(overrideGateway);
        scope.ServiceProvider.GetRequiredService<ITenantsBffComposition>()
            .IsReadSurfaceConnected.ShouldBeTrue();
    }

    [Fact]
    public void Partial_host_query_gateway_override_is_rejected_during_composition()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddSingleton(Substitute.For<ITenantQueryGateway>());

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false));

        exception.Message.ShouldContain(nameof(ITenantsReadSurfaceAvailability));
    }

    [Fact]
    public void Partial_host_read_availability_override_is_rejected_during_composition()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddSingleton<ITenantsReadSurfaceAvailability>(
            new TenantsReadSurfaceAvailability(IsConnected: true));

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false));

        exception.Message.ShouldContain(nameof(ITenantQueryGateway));
    }

    [Fact]
    public void Keyed_gateway_registration_does_not_count_as_the_unkeyed_host_override()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        ITenantQueryGateway keyedGateway = Substitute.For<ITenantQueryGateway>();
        services.AddKeyedSingleton("sidecar", keyedGateway);

        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantQueryGateway>()
            .ShouldBeOfType<UnavailableTenantQueryGateway>();
        scope.ServiceProvider.GetRequiredKeyedService<ITenantQueryGateway>("sidecar")
            .ShouldBeSameAs(keyedGateway);
    }

    /// <summary>
    /// The Aspire service-discovery compound base-address form fails closed on its OWN side.
    /// </summary>
    /// <remarks>
    /// Review loop 9, decision D2. These two tests previously asserted that a compound address made
    /// <c>AddHexalithTenantsUiModule</c> THROW. That violated AC2 and the "Separate dependencies" matrix row
    /// -- the read-side guard ran before the command block, so an unsendable read address also prevented
    /// <c>AddHexalithEventStore</c>, <c>AddEventStoreGatewayClient</c> and <c>TenantCommandGateway</c> from
    /// registering at all, and the mirror case killed the read side. It was also inconsistent with
    /// <c>Malformed_or_non_http_tenants_base_address_fails_closed</c> in this same class, which asserts the
    /// per-side behaviour for malformed values. <c>TryGetHttpBaseAddress</c> already rejects the compound
    /// scheme (<c>IsSendableScheme</c> accepts only exact http/https), so nothing is lost by not throwing.
    /// </remarks>
    [Theory]
    [InlineData("https+http://tenants")]
    [InlineData("http+https://tenants")]
    [InlineData("https+http://tenants.invalid:8443")]
    public async Task Compound_tenants_address_fails_closed_without_disabling_the_command_side(string baseAddress)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:BaseAddress"] = baseAddress,
                ["EventStore:BaseAddress"] = "https://eventstore.invalid",
            })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        var capture = new CapturingLoggerProvider(captureAll: true);
        services.AddLogging(logging => logging.AddProvider(capture));

        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();

        // Read side fails closed...
        scope.ServiceProvider.GetRequiredService<ITenantQueryGateway>()
            .ShouldBeOfType<UnavailableTenantQueryGateway>();
        scope.ServiceProvider.GetRequiredService<ITenantsReadSurfaceAvailability>()
            .IsConnected.ShouldBeFalse();

        // ...and the command side, which has a perfectly good reference, still registers.
        scope.ServiceProvider.GetService<TenantCommandGateway>().ShouldNotBeNull();

        // ...and the rejection is recorded by KEY NAME, which is the whole point of decision D-G: without a
        // signal on any channel a typo and a genuine outage render the identical "read surface unavailable"
        // state. This module strips HTTP client loggers on purpose, so nothing else reports it.
        TenantsUiConfigurationDiagnostics diagnostics = provider
            .GetServices<TenantsUiConfigurationDiagnostics>()
            .ShouldHaveSingleItem();
        diagnostics.RejectedBaseAddressSettings.ShouldBe(["Tenants:BaseAddress"]);

        // The configured value can carry host, path, query or user-info material and must never reach a log
        // or telemetry channel. Only the key name is retained.
        diagnostics.RejectedBaseAddressSettings.ShouldNotContain(baseAddress);
        string.Join("|", diagnostics.RejectedBaseAddressSettings).ShouldNotContain("tenants.invalid");

        await StartConfigurationDiagnosticsReporterAsync(provider);
        capture.Messages.Count(message => message.Contains("Tenants:BaseAddress", StringComparison.Ordinal))
            .ShouldBe(1);
        string logged = string.Join("|", capture.Messages);
        logged.ShouldNotContain(baseAddress);
        logged.ShouldNotContain("tenants.invalid");
    }

    /// <summary>
    /// An absent base address is not a misconfiguration and must not be reported as one.
    /// </summary>
    /// <remarks>
    /// Failing closed to the unavailable gateway is the intended outcome when a dependency is simply not
    /// wired. Only a value the operator believed would work — present, but unusable — is worth a warning.
    /// Inverting the recording condition passed the whole suite before this test existed.
    /// </remarks>
    [Fact]
    public void An_unconfigured_base_address_is_not_recorded_as_a_rejection()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EventStore:BaseAddress"] = "https://eventstore.invalid",
            })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);

        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITenantQueryGateway>()
            .ShouldBeOfType<UnavailableTenantQueryGateway>();
        provider.GetServices<TenantsUiConfigurationDiagnostics>()
            .SelectMany(static entry => entry.RejectedBaseAddressSettings)
            .ShouldBeEmpty();
    }

    /// <summary>
    /// A host that pre-registers the diagnostics type must not silence the composition's own findings.
    /// </summary>
    /// <remarks>
    /// The registration used <c>TryAddSingleton</c>, so a pre-registered instance won and the reporter — a
    /// singleton hosted service — resolved an empty one, restoring exactly the silent state D-G removed.
    /// The reporter now consumes the whole enumerable, so every composition's findings survive.
    /// </remarks>
    [Fact]
    public async Task A_preregistered_diagnostics_instance_does_not_discard_this_compositions_rejections()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:BaseAddress"] = "https+http://tenants",
                ["EventStore:BaseAddress"] = "https://eventstore.invalid",
            })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddSingleton(new TenantsUiConfigurationDiagnostics());
        var capture = new CapturingLoggerProvider(captureAll: true);
        services.AddLogging(logging => logging.AddProvider(capture));

        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetServices<TenantsUiConfigurationDiagnostics>()
            .SelectMany(static entry => entry.RejectedBaseAddressSettings)
            .ShouldBe(["Tenants:BaseAddress"]);

        await StartConfigurationDiagnosticsReporterAsync(provider);
        capture.Messages.Count(message => message.Contains("Tenants:BaseAddress", StringComparison.Ordinal))
            .ShouldBe(1);
        string.Join("|", capture.Messages).ShouldNotContain("https+http://tenants");
    }

    [Fact]
    public async Task Rejections_from_two_module_registrations_are_both_reported_once()
    {
        IConfiguration readRejected = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:BaseAddress"] = "https+http://tenants",
                ["EventStore:BaseAddress"] = "https://eventstore.invalid",
            })
            .Build();
        IConfiguration commandRejected = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:BaseAddress"] = "https://tenants.invalid",
                ["EventStore:BaseAddress"] = "https+http://eventstore",
            })
            .Build();
        ServiceCollection services = new();
        var capture = new CapturingLoggerProvider(captureAll: true);
        services.AddLogging(logging => logging.AddProvider(capture));

        services.AddHexalithTenantsUiModule(readRejected, enableGatewayAuthorization: false);
        services.AddHexalithTenantsUiModule(commandRejected, enableGatewayAuthorization: false);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        // Review loop 13. Every gateway registration is TryAdd*, so the FIRST call wins: the read side is
        // the unavailable gateway call 1 registered, and the command side is the WORKING gateway call 1
        // registered from its usable EventStore address. Call 2's rejected EventStore key therefore
        // describes a registration that never took effect, and reporting it told the operator a working
        // surface was unavailable. Only the rejection that governs the resolved container is reported.
        provider.GetServices<TenantsUiConfigurationDiagnostics>()
            .SelectMany(static entry => entry.RejectedBaseAddressSettings)
            .ShouldBe(["Tenants:BaseAddress"]);

        // Resolve what the container actually holds, which the previous form never did -- it asserted the
        // diagnostic list alone, so the warning could contradict the composition and still pass.
        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantQueryGateway>()
            .ShouldBeOfType<UnavailableTenantQueryGateway>();
        scope.ServiceProvider.GetRequiredService<ITenantCommandGateway>()
            .ShouldNotBeOfType<UnavailableTenantCommandGateway>();

        await StartConfigurationDiagnosticsReporterAsync(provider);
        capture.Messages.Count(message => message.Contains("Tenants:BaseAddress", StringComparison.Ordinal))
            .ShouldBe(1);
        capture.Messages.Count(message => message.Contains("EventStore:BaseAddress", StringComparison.Ordinal))
            .ShouldBe(0);
        string logged = string.Join("|", capture.Messages);
        logged.ShouldNotContain("https+http://tenants");
        logged.ShouldNotContain("https+http://eventstore");
    }

    [Fact]
    public async Task Compound_event_store_address_fails_closed_without_disabling_the_independent_read_side()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:BaseAddress"] = "https://tenants.invalid",
                ["EventStore:BaseAddress"] = "https+http://eventstore",
            })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        var capture = new CapturingLoggerProvider(captureAll: true);
        services.AddLogging(logging => logging.AddProvider(capture));

        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();

        // The read side has its own valid reference and must stay connected.
        scope.ServiceProvider.GetRequiredService<ITenantQueryGateway>()
            .ShouldBeOfType<TenantQueryGateway>();
        scope.ServiceProvider.GetRequiredService<ITenantsReadSurfaceAvailability>()
            .IsConnected.ShouldBeTrue();

        // The command side fails closed on its own reference.
        scope.ServiceProvider.GetService<TenantCommandGateway>().ShouldBeNull();

        TenantsUiConfigurationDiagnostics diagnostics = provider
            .GetServices<TenantsUiConfigurationDiagnostics>()
            .ShouldHaveSingleItem();
        diagnostics.RejectedBaseAddressSettings.ShouldBe(["EventStore:BaseAddress"]);

        await StartConfigurationDiagnosticsReporterAsync(provider);
        capture.Messages.Count(message => message.Contains("EventStore:BaseAddress", StringComparison.Ordinal))
            .ShouldBe(1);
        string.Join("|", capture.Messages).ShouldNotContain("https+http://eventstore");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Read_and_command_dependencies_register_independently(
        bool hasTenants,
        bool hasEventStore)
    {
        var settings = new Dictionary<string, string?>();
        if (hasTenants)
        {
            settings["Tenants:BaseAddress"] = "https://tenants.invalid";
        }

        if (hasEventStore)
        {
            settings["EventStore:BaseAddress"] = "https://eventstore.invalid";
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        ServiceCollection services = new();

        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        services.AddSingleton(configuration);
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();

        ITenantQueryGateway query = scope.ServiceProvider.GetRequiredService<ITenantQueryGateway>();
        ITenantCommandGateway command = scope.ServiceProvider.GetRequiredService<ITenantCommandGateway>();
        query.ShouldBeOfType(hasTenants ? typeof(TenantQueryGateway) : typeof(UnavailableTenantQueryGateway));
        command.ShouldBeOfType(hasEventStore ? typeof(TenantCommandGateway) : typeof(UnavailableTenantCommandGateway));
        (scope.ServiceProvider.GetService<ITenantsRestQueryClient>() is not null).ShouldBe(hasTenants);
    }

    [Theory]
    [InlineData("http++https://tenants.invalid")]
    [InlineData("ftp://tenants.invalid")]
    [InlineData("file:///tmp/tenants")]
    [InlineData("https://user:secret@tenants.invalid")]
    [InlineData("https://tenants.invalid?target=other")]
    [InlineData("https://tenants.invalid#fragment")]
    [InlineData("https:/missing-host")]
    [InlineData("not an absolute URI")]
    public void Malformed_or_non_http_tenants_base_address_fails_closed(string baseAddress)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:BaseAddress"] = baseAddress,
                ["EventStore:BaseAddress"] = "https://eventstore.invalid",
            })
            .Build();
        ServiceCollection services = new();

        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        ServiceDescriptor query = services.Last(descriptor => descriptor.ServiceType == typeof(ITenantQueryGateway));
        query.ImplementationType.ShouldBe(typeof(UnavailableTenantQueryGateway));
        services.Any(descriptor => descriptor.ServiceType == typeof(ITenantsRestQueryClient)).ShouldBeFalse();
        services.Last(descriptor => descriptor.ServiceType == typeof(ITenantCommandGateway))
            .ImplementationFactory.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("http++https://eventstore.invalid")]
    [InlineData("ftp://eventstore.invalid")]
    [InlineData("https://user:secret@eventstore.invalid")]
    [InlineData("https://eventstore.invalid?target=other")]
    [InlineData("https://eventstore.invalid#fragment")]
    [InlineData("https:/missing-host")]
    public void Invalid_event_store_address_does_not_disable_the_independent_read_gateway(string baseAddress)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:BaseAddress"] = "https://tenants.invalid",
                ["EventStore:BaseAddress"] = baseAddress,
            })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITenantQueryGateway>()
            .ShouldBeOfType<TenantQueryGateway>();
        scope.ServiceProvider.GetRequiredService<ITenantCommandGateway>()
            .ShouldBeOfType<UnavailableTenantCommandGateway>();
        ITenantsBffComposition composition = scope.ServiceProvider.GetRequiredService<ITenantsBffComposition>();
        composition.IsReadSurfaceConnected.ShouldBeTrue();
        composition.IsCommandSurfaceConnected.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "Bearer direct-read-token")]
    public async Task Direct_tenants_client_relays_the_server_side_bearer_only_when_authorization_is_enabled(
        bool enableGatewayAuthorization,
        string? expectedAuthorization)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:BaseAddress"] = "https://tenants.invalid",
            })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddHexalithFrontComposerTokenRelay();
        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization);
        var primaryHandler = new AuthorizationRecordingHandler();
        services.AddHttpClient<TenantsRestQueryClient>()
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "operator-user")],
                authenticationType: "test")),
        };
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });

        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<FrontComposerUserTokenStore>().Set(
            "operator-user",
            "direct-read-token",
            DateTimeOffset.UtcNow.AddMinutes(5));
        using IServiceScope scope = provider.CreateScope();
        ITenantsRestQueryClient client = scope.ServiceProvider.GetRequiredService<ITenantsRestQueryClient>();

        _ = await client.ListTenantsAsync(
            new ListTenantsQuery { PageSize = 20 },
            null,
            TestContext.Current.CancellationToken);

        primaryHandler.Authorization.ShouldBe(expectedAuthorization);
    }

    [Fact]
    public void Configuration_policy_services_are_idempotent_and_do_not_make_missing_policy_startup_fatal()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);
        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        services.Count(descriptor => descriptor.ServiceType == typeof(ITenantConfigurationPrincipalResolver)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(TenantConfigurationReadPolicyProvider)).ShouldBe(1);

        using ServiceProvider provider = services.BuildServiceProvider();
        TenantConfigurationReadPolicyProvider policyProvider = provider.GetRequiredService<TenantConfigurationReadPolicyProvider>();
        TenantConfigurationReadPolicyResolution resolution = policyProvider.Resolve(
            "tenant.alpha",
            TenantConfigurationPrincipalEvidence.NonAdministrator("operator-user"));

        resolution.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public void Configuration_policy_prefers_the_host_root_when_registration_receives_a_subsection()
    {
        IConfiguration root = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:ConfigurationReadPolicy:PrefixGrants:0:TenantId"] = "tenant.alpha",
                ["Tenants:ConfigurationReadPolicy:PrefixGrants:0:Subject"] = "operator-user",
                ["Tenants:ConfigurationReadPolicy:PrefixGrants:0:Prefix"] = "billing",
                ["Tenants:ConfigurationReadPolicy:DisplaySafe:0"] = "billing.mode",
            })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(root);

        services.AddTenantConfigurationReadPolicy(root.GetSection("Tenants"));

        using ServiceProvider provider = services.BuildServiceProvider();
        TenantConfigurationReadPolicyResolution resolution = provider
            .GetRequiredService<TenantConfigurationReadPolicyProvider>()
            .Resolve(
                "tenant.alpha",
                TenantConfigurationPrincipalEvidence.NonAdministrator("operator-user"));

        resolution.IsAvailable.ShouldBeTrue();
        resolution.AuthorizedPrefixes.ShouldBe(["billing"]);
    }

    [Fact]
    public void Tenant_search_composition_is_purpose_isolated_scoped_and_server_configured()
    {
        ServiceCollection services = new();

        // Purpose isolation is proven with ONE Data Protection provider so only the codec purposes vary;
        // two providers would prove key-ring separation instead.
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var hostCodec = new QueryCursorCodec(dataProtectionProvider, "host-purpose");
        services.AddSingleton<IDataProtectionProvider>(dataProtectionProvider);
        services.AddSingleton<IQueryCursorCodec>(hostCodec);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EventStore:BaseAddress"] = "https://eventstore.invalid",
                ["Memories:BaseAddress"] = "https://memories.invalid",
                ["HEXALITH_MEMORIES_API_TOKEN"] = "server-only-token",
            })
            .Build();

        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        provider.GetRequiredService<IDataProtectionProvider>().ShouldBeSameAs(dataProtectionProvider);
        provider.GetRequiredService<IQueryCursorCodec>().ShouldBeSameAs(hostCodec);
        ITenantSearchCursorCodec searchCodec = provider.GetRequiredService<ITenantSearchCursorCodec>();
        provider.GetRequiredService<ITenantSearchCursorCodec>().ShouldBeSameAs(searchCodec);
        string scope = TenantSearchCursorScopes.Create(
            "operator-user",
            "needle",
            status: null,
            TenantListSortColumns.TenantId,
            descending: false,
            pageSize: 20);
        string protectedCursor = searchCodec.Encode(scope, 20);
        hostCodec.TryDecode(
            protectedCursor,
            TenantSearchCursorPosition.QueryType,
            scope,
            out _,
            out _).ShouldBeFalse();
        searchCodec.TryDecode(protectedCursor, scope, out int decodedOffset).ShouldBeTrue();
        decodedOffset.ShouldBe(20);

        // Purpose isolation was previously inferred from the *container's* provider identity, which says
        // nothing about which provider the codec actually received. A codec that ignored injection and
        // built its own EphemeralDataProtectionProvider passed every assertion while making each cursor
        // undecodable after a restart or on a second replica. Prove the injected provider is the one in
        // use: an independent codec over the same provider must decode this cursor, and the same codec
        // over a different provider must not.
        var sameProviderCodec = new TenantSearchCursorCodec(dataProtectionProvider);
        sameProviderCodec.TryDecode(protectedCursor, scope, out int sameProviderOffset).ShouldBeTrue(
            "the registered codec must protect with the injected provider, not one it constructed itself");
        sameProviderOffset.ShouldBe(20);

        var foreignProviderCodec = new TenantSearchCursorCodec(new EphemeralDataProtectionProvider());
        foreignProviderCodec.TryDecode(protectedCursor, scope, out _).ShouldBeFalse();

        using IServiceScope firstScope = provider.CreateScope();
        using IServiceScope secondScope = provider.CreateScope();
        TenantSearchPagingState firstPaging = firstScope.ServiceProvider.GetRequiredService<TenantSearchPagingState>();
        firstPaging.ShouldBeSameAs(firstScope.ServiceProvider.GetRequiredService<TenantSearchPagingState>());
        firstPaging.ShouldNotBeSameAs(secondScope.ServiceProvider.GetRequiredService<TenantSearchPagingState>());

        MemoriesClient memories = firstScope.ServiceProvider.GetRequiredService<MemoriesClient>();
        memories.BaseAddress.ShouldBe(new Uri("https://memories.invalid"));
        MemoriesClientOptions options = provider.GetRequiredService<IOptions<MemoriesClientOptions>>().Value;
        options.Endpoint.ShouldBe(new Uri("https://memories.invalid"));
        options.ApiToken.ShouldBe("server-only-token");
    }

    [Fact]
    public async Task Embedded_ui_module_emits_no_default_memories_http_logs_that_could_carry_query_or_offset()
    {
        CapturingLoggerProvider capture = new();
        ServiceCollection services = new();
        services.AddLogging(builder =>
        {
            _ = builder.SetMinimumLevel(LogLevel.Trace);
            _ = builder.AddProvider(capture);
        });
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Memories:BaseAddress"] = "https://memories.invalid",
            })
            .Build();
        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);
        AddStubPrimaryHandler(services, MemoriesHttpClientName);
        AddStubPrimaryHandler(services, ControlHttpClientName);

        using ServiceProvider provider = services.BuildServiceProvider();

        await AssertMemoriesHttpLoggingIsSuppressedAsync(provider, capture);
    }

    [Fact]
    public async Task Registered_tenants_client_emits_no_default_http_logs_that_could_carry_a_protected_cursor()
    {
        const string sentinelCursor = "protected-cursor-sentinel";
        CapturingLoggerProvider capture = new();
        ServiceCollection services = new();
        services.AddLogging(builder =>
        {
            _ = builder.SetMinimumLevel(LogLevel.Trace);
            _ = builder.AddProvider(capture);
        });
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:BaseAddress"] = "https://tenants.invalid",
            })
            .Build();
        services.AddSingleton(configuration);
        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);
        var primaryHandler = new AuthorizationRecordingHandler();
        services.AddHttpClient<TenantsRestQueryClient>()
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler);
        AddStubPrimaryHandler(services, ControlHttpClientName);

        using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        using (HttpClient control = factory.CreateClient(ControlHttpClientName))
        {
            using HttpResponseMessage controlResponse = await control.GetAsync(
                new Uri($"https://tenants.invalid/api/v1/tenants?cursor={sentinelCursor}"));
            controlResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        capture.Messages.ShouldNotBeEmpty();
        capture.Clear();

        using IServiceScope scope = provider.CreateScope();
        ITenantsRestQueryClient client = scope.ServiceProvider.GetRequiredService<ITenantsRestQueryClient>();
        TenantsRestQueryResponse<PaginatedResult<TenantSummary>> response = await client.ListTenantsAsync(
            new ListTenantsQuery { Cursor = sentinelCursor, PageSize = 20 },
            eTag: null,
            TestContext.Current.CancellationToken);

        response.IsSuccess.ShouldBeTrue();
        primaryHandler.RequestUri.ShouldNotBeNull().Query.ShouldContain(sentinelCursor);
        capture.Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task Standalone_ui_host_resolves_the_same_server_side_search_composition()
    {
        CapturingLoggerProvider capture = new();
        await using WebApplicationFactory<global::Program> baseFactory = new();
        using WebApplicationFactory<global::Program> factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddLogging(logging =>
                {
                    _ = logging.SetMinimumLevel(LogLevel.Trace);
                    _ = logging.AddProvider(capture);
                });
                AddStubPrimaryHandler(services, MemoriesHttpClientName);
                AddStubPrimaryHandler(services, ControlHttpClientName);
            }));

        ITenantSearchCursorCodec searchCodec = factory.Services.GetRequiredService<ITenantSearchCursorCodec>();
        searchCodec.ShouldBeSameAs(factory.Services.GetRequiredService<ITenantSearchCursorCodec>());

        using IServiceScope firstScope = factory.Services.CreateScope();
        using IServiceScope secondScope = factory.Services.CreateScope();
        firstScope.ServiceProvider.GetRequiredService<TenantSearchPagingState>()
            .ShouldNotBeSameAs(secondScope.ServiceProvider.GetRequiredService<TenantSearchPagingState>());

        // The query gateway takes an optional reason-code logger; resolving it proves the added parameter
        // still satisfies the container's constructor selection in a real host.
        firstScope.ServiceProvider.GetRequiredService<ITenantQueryGateway>().ShouldNotBeNull();

        // Lifetimes alone hold for any registration whatsoever. The standalone host protects real cursors,
        // so the substance the embedded-module test proves is proven here too, against the real host: a
        // round trip, scope binding, protection by the host's own Data Protection provider rather than one
        // the codec built for itself, and a search purpose that is not interchangeable with the generic
        // query-cursor purpose.
        string scope = TenantSearchCursorScopes.Create(
            "operator-user",
            "needle",
            status: null,
            TenantListSortColumns.TenantId,
            descending: false,
            pageSize: 20);
        string protectedCursor = searchCodec.Encode(scope, 20);
        searchCodec.TryDecode(protectedCursor, scope, out int decodedOffset).ShouldBeTrue();
        decodedOffset.ShouldBe(20);
        searchCodec.TryDecode(protectedCursor, scope + "-other", out _).ShouldBeFalse();

        IDataProtectionProvider hostProvider = factory.Services.GetRequiredService<IDataProtectionProvider>();
        new TenantSearchCursorCodec(hostProvider)
            .TryDecode(protectedCursor, scope, out int hostProviderOffset)
            .ShouldBeTrue("the registered codec must protect with the host's provider, not one it constructed");
        hostProviderOffset.ShouldBe(20);
        new TenantSearchCursorCodec(new EphemeralDataProtectionProvider())
            .TryDecode(protectedCursor, scope, out _)
            .ShouldBeFalse();
        new QueryCursorCodec(hostProvider, "host-purpose")
            .TryDecode(protectedCursor, TenantSearchCursorPosition.QueryType, scope, out _, out _)
            .ShouldBeFalse();

        await AssertMemoriesHttpLoggingIsSuppressedAsync(factory.Services, capture);
    }

    [Fact]
    public void Configured_tenants_client_disables_redirects_on_its_actual_primary_handler()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:BaseAddress"] = "https://tenants.invalid",
            })
            .Build();
        ServiceCollection services = new();
        PrimaryHandlerCapture capture = new();
        services.AddSingleton<IHttpMessageHandlerBuilderFilter>(capture);
        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<ITenantsRestQueryClient>();

        HttpClientHandler handler = capture.PrimaryHandler
            .ShouldBeOfType<HttpClientHandler>(
                "the configured Tenants typed client must build the primary handler whose redirect policy ships");
        handler.AllowAutoRedirect.ShouldBeFalse(
            "a redirect must remain a 3xx failure at the exact-route client rather than being followed to another resource");
    }

    [Fact]
    public void Authoritative_search_resources_resolve_complete_english_and_french_copy()
    {
        ResourceManager manager = new(typeof(TenantsResources));
        Dictionary<string, (string English, string French)> expected = new(StringComparer.Ordinal)
        {
            ["Tenants.List.Notice.SearchUnavailable"] = (
                "Protected whole-set search is temporarily unavailable. You can continue browsing the authorized tenant list.",
                "La recherche protégée sur l'ensemble des locataires est temporairement indisponible. Vous pouvez continuer à parcourir la liste autorisée."),
            // Not "the page was no longer available": the browser-Back path discards a position that was
            // still perfectly available, because the return context could not be validated. The copy has to
            // be true of every trigger that raises it.
            ["Tenants.List.Notice.SearchRefreshed"] = (
                "Protected search paging could not be restored. Search has restarted from the first page.",
                "La pagination de recherche protégée n'a pas pu être restaurée. La recherche a redémarré depuis la première page."),
            ["Tenants.List.Notice.SearchAndListUnavailable"] = (
                "Protected whole-set search is temporarily unavailable, and the authorized tenant list could not be loaded either. Try again later.",
                "La recherche protégée sur l'ensemble des locataires est temporairement indisponible, et la liste autorisée n'a pas pu être chargée non plus. Réessayez plus tard."),
            ["Tenants.List.Notice.SearchTermTooLong"] = (
                "The search term was too long to apply, so the full authorized tenant list is shown. Shorten the term and search again.",
                "Le terme recherché était trop long pour être appliqué, la liste autorisée complète est donc affichée. Raccourcissez le terme et relancez la recherche."),
            ["Tenants.List.Notice.SearchPagingRestarted"] = (
                "The available tenant source changed. Paging restarted from the first page.",
                "La source de locataires disponible a changé. La pagination a redémarré depuis la première page."),
            ["Tenants.List.State.SearchPageEmpty.Title"] = (
                "No tenants match this search",
                "Aucun locataire ne correspond à cette recherche"),

            // One message for both causes. A window that yields no authorized row and a window that
            // matched nothing must be described identically, or the copy itself discloses that hidden
            // candidates existed.
            ["Tenants.List.State.SearchPageEmpty.Message"] = (
                "No tenants you can access match this search. Check the search term, or clear it to return to the full list.",
                "Aucun locataire auquel vous avez accès ne correspond à cette recherche. Vérifiez le terme recherché ou effacez-le pour revenir à la liste complète."),
            ["Tenants.List.State.SearchPageEmpty.MoreTitle"] = (
                "No visible tenants on this search page",
                "Aucun locataire visible sur cette page de recherche"),
            ["Tenants.List.State.SearchPageEmpty.MoreMessage"] = (
                "No authorized tenant results are visible on this page. Continue to the next search page to check for more results, or clear the search to return to the full list.",
                "Aucun résultat de locataire autorisé n’est visible sur cette page. Passez à la page de recherche suivante pour vérifier s’il existe d’autres résultats, ou effacez la recherche pour revenir à la liste complète."),
            ["Tenants.List.Reason.SearchPartiallyAvailable"] = (
                "Some search results could not be verified. Only authorized tenant rows that were verified are shown.",
                "Certains résultats de recherche n'ont pas pu être vérifiés. Seules les lignes de locataire autorisées et vérifiées sont affichées."),
            ["Tenants.List.StatusFilterLabel.Authoritative"] = (
                "Status across indexed candidates",
                "Statut parmi les candidats indexés"),
            ["Tenants.List.AuthoritativeSearchSemantics"] = (
                "Search and status apply across indexed tenant candidates. Only authorized, verified tenant rows are shown; sorting applies within this protected page.",
                "La recherche et le statut s'appliquent aux candidats de locataire indexés. Seules les lignes autorisées et vérifiées sont affichées ; le tri s'applique dans cette page protégée."),
        };

        foreach ((string key, (string english, string french)) in expected)
        {
            manager.GetString(key, CultureInfo.InvariantCulture).ShouldBe(english);
            manager.GetString(key, CultureInfo.GetCultureInfo("fr")).ShouldBe(french);
        }
    }

    [Fact]
    public void Bff_composition_reflects_lifecycle_authority_from_server_side_global_admin_principal()
    {
        ITenantsBffComposition composition = new TenantsBffComposition(
            new StubTenantCommandGateway(),
            ContextAccessor(
                new Claim("sub", "operator.alpha"),
                new Claim("eventstore:tenant", "system"),
                new Claim(ClaimTypes.Role, "GlobalAdministrator")));

        composition.LifecycleAuthorizationReflection.ShouldBe(TenantLifecycleAuthorizationReflectionState.Authorized);
        composition.GlobalAdministratorsAuthorizationReflection.ShouldBe(TenantLifecycleAuthorizationReflectionState.Authorized);
    }

    [Fact]
    public void Bff_composition_reflects_global_admin_boolean_claim_from_keycloak_mapper()
    {
        ITenantsBffComposition composition = new TenantsBffComposition(
            new StubTenantCommandGateway(),
            ContextAccessor(
                new Claim("sub", "operator.alpha"),
                new Claim("eventstore:tenant", "system"),
                new Claim("global_admin", "true")));

        composition.LifecycleAuthorizationReflection.ShouldBe(TenantLifecycleAuthorizationReflectionState.Authorized);
        composition.GlobalAdministratorsAuthorizationReflection.ShouldBe(TenantLifecycleAuthorizationReflectionState.Authorized);
    }

    [Fact]
    public void Global_administrator_claim_helper_matches_navigation_and_bff_authorization_shapes()
    {
        TenantsGlobalAdministratorClaims.IsGlobalAdministrator(Principal(
            new Claim("sub", "operator.alpha"),
            new Claim("eventstore:tenant", "system"),
            new Claim("global_admin", "true"))).ShouldBeTrue();
        TenantsGlobalAdministratorClaims.IsGlobalAdministrator(Principal(
            new Claim("sub", "operator.alpha"),
            new Claim("eventstore:tenant", "system"),
            new Claim("roles", "[\"tenant-reader\",\"global-admin\"]"))).ShouldBeTrue();
        // ClaimTypes.Role must accept the same JSON-array shape that `roles` already accepts.
        TenantsGlobalAdministratorClaims.IsGlobalAdministrator(Principal(
            new Claim("sub", "operator.alpha"),
            new Claim("eventstore:tenant", "system"),
            new Claim(ClaimTypes.Role, "[\"tenant-reader\",\"global-admin\"]"))).ShouldBeTrue();
        TenantsGlobalAdministratorClaims.IsGlobalAdministrator(Principal(
            new Claim("sub", "operator.alpha"),
            new Claim("eventstore:tenant", "system"),
            new Claim("role", "[\"global-admin\"]"))).ShouldBeTrue();
        TenantsGlobalAdministratorClaims.IsGlobalAdministrator(Principal(
            new Claim("sub", "operator.alpha"),
            new Claim("eventstore:tenant", "system"),
            new Claim(ClaimTypes.Role, "GlobalAdministrator"))).ShouldBeTrue();

        TenantsGlobalAdministratorClaims.IsGlobalAdministrator(Principal(
            new Claim("sub", "operator.alpha"),
            new Claim("eventstore:tenant", "system"),
            new Claim("global_admin", "false"))).ShouldBeFalse();
        TenantsGlobalAdministratorClaims.IsGlobalAdministrator(Principal(
            new Claim("sub", "operator.alpha"),
            new Claim("global_admin", "true"))).ShouldBeFalse();
        TenantsGlobalAdministratorClaims.IsGlobalAdministrator(Principal(false,
            new Claim("sub", "operator.alpha"),
            new Claim("eventstore:tenant", "system"),
            new Claim("global_admin", "true"))).ShouldBeFalse();
    }

    [Fact]
    public void Bff_composition_fails_closed_for_global_admin_shape_without_system_tenant_claim()
    {
        ITenantsBffComposition composition = new TenantsBffComposition(
            new StubTenantCommandGateway(),
            ContextAccessor(
                new Claim("sub", "operator.alpha"),
                new Claim(ClaimTypes.Role, "GlobalAdministrator")));

        composition.LifecycleAuthorizationReflection.ShouldBe(TenantLifecycleAuthorizationReflectionState.MissingPermission);
        composition.GlobalAdministratorsAuthorizationReflection.ShouldBe(TenantLifecycleAuthorizationReflectionState.MissingPermission);
    }

    [Fact]
    public void Global_administrators_read_contract_uses_fixed_platform_scope_without_tenant_substitute()
    {
        string contractsQueryRoot = Path.Combine(ProjectRoot(), "src", "Hexalith.Tenants.Contracts", "Queries");
        string[] queryFiles = Directory.GetFiles(contractsQueryRoot, "*.cs", SearchOption.TopDirectoryOnly);
        queryFiles.Select(Path.GetFileName).ShouldContain("GetGlobalAdministratorsQuery.cs");
        queryFiles.Select(Path.GetFileName).ShouldNotContain("ListGlobalAdministratorsQuery.cs");

        File.Exists(Path.Combine(ProjectRoot(), "src", "Hexalith.Tenants", "Controllers", "TenantsQueryController.cs"))
            .ShouldBeFalse();
        string apiAssemblyInfo = File.ReadAllText(
            Path.Combine(ProjectRoot(), "src", "Hexalith.Tenants.Api", "RestApiAssemblyInfo.cs"));
        apiAssemblyInfo.ShouldContain("RestApi(\"api/tenants\", \"tenants\", RestTenantSource.System)");
        string globalAdministratorsQuery = File.ReadAllText(
            Path.Combine(contractsQueryRoot, "GetGlobalAdministratorsQuery.cs"));
        globalAdministratorsQuery.ShouldContain("[RestRoute(RestVerb.Get, \"~/api/global-administrators\", ApiScope = \"tenants\")]");
        globalAdministratorsQuery.ShouldContain("RestQueryBindingSource.Constant, \"global-administrators\"");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TenantsUiProject_DoesNotHostGeneratedRestApiOrReferenceExternalApiHost(bool useProjectReferences)
    {
        string uiRoot = Path.Combine(ProjectRoot(), "src", "Hexalith.Tenants.UI");
        string uiProjectPath = Path.Combine(uiRoot, "Hexalith.Tenants.UI.csproj");

        // Each resolved dependency mode is asserted on its own. Unioning the two modes let a
        // non-empty result from one satisfy the positive assertion for both, and let every negative
        // assertion pass vacuously whenever a mode returned nothing at all.
        string[] dependencies = await ReadResolvedDependencyValuesAsync(uiProjectPath, useProjectReferences);

        dependencies.ShouldNotBeEmpty(
            $"MSBuild resolution returned no dependency items for UseHexalithProjectReferences={useProjectReferences}; "
            + "every assertion below would pass vacuously.");
        dependencies.ShouldContain(
            static dependency => MatchesDependencyIdentity(dependency, "Hexalith.Tenants.Client"),
            "Interactive Tenants UI must consume the approved typed Tenants client seam.");
        dependencies.Where(static dependency => MatchesDependencyIdentity(dependency, "Hexalith.Tenants.Api"))
            .ShouldBeEmpty("Interactive Tenants UI must not reference the external generated API host.");
        dependencies.Where(static dependency => MatchesDependencyIdentity(dependency, "Hexalith.EventStore.RestApi.Generators"))
            .ShouldBeEmpty("Generated REST analyzers belong only in Hexalith.Tenants.Api.");
        dependencies.Where(static dependency => MatchesDependencyIdentity(dependency, "Hexalith.Tenants"))
            .ShouldBeEmpty("Interactive Tenants UI must not reference the Tenants domain-service host.");
    }

    /// <summary>
    /// Endpoint authorization metadata is safe only when an authentication scheme is composed alongside it.
    /// The module always registers authorization services, but the standalone host registers an
    /// authentication scheme only when OIDC is configured. On that Keycloak-disabled topology an
    /// <c>[Authorize]</c> attribute makes <c>WebApplication</c> insert the authorization middleware, whose
    /// challenge path throws <see cref="InvalidOperationException"/> for the missing
    /// <c>IAuthenticationService</c> — the route answers 500 instead of rendering its fail-closed state.
    /// <para>
    /// This is a pairing invariant, not a permanent architectural ban. It is enforced only while the
    /// composed module resolves no <c>IAuthenticationService</c>. A host that configures OIDC — or a future
    /// story that composes an authentication scheme in the module — restores endpoint authorization as
    /// defence-in-depth without changing this test, because the guard below simply stops applying.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Privileged_route_carries_endpoint_authorization_only_when_an_authentication_scheme_is_composed(
        bool oidcEnabled)
    {
        await using WebApplicationFactory<global::Program> baseFactory = new();
        using WebApplicationFactory<global::Program> factory = baseFactory.WithWebHostBuilder(builder =>
        {
            if (oidcEnabled)
            {
                _ = builder.UseSetting(
                    "Authentication:OpenIdConnect:Authority",
                    "https://keycloak.invalid/realms/hexalith");
                _ = builder.UseSetting("Authentication:OpenIdConnect:ClientId", "tenants-ui-tests");
                _ = builder.UseSetting("Authentication:OpenIdConnect:ClientSecret", "test-only-secret");
            }
        });

        using IServiceScope serviceScope = factory.Services.CreateScope();
        bool hasAuthenticationService = serviceScope.ServiceProvider.GetService<IAuthenticationService>() is not null;
        hasAuthenticationService.ShouldBe(oidcEnabled,
            "the endpoint metadata assertion must be paired with the topology that can enforce it");

        Endpoint[] privilegedEndpoints = factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(static source => source.Endpoints)
            .Where(static endpoint => endpoint.Metadata.GetMetadata<ComponentTypeMetadata>()?.Type
                == typeof(GlobalAdministratorsPage))
            .ToArray();
        privilegedEndpoints.ShouldNotBeEmpty(
            "the host must materialize the real /global-administrators component endpoint before its metadata can be asserted");

        foreach (Endpoint endpoint in privilegedEndpoints)
        {
            IAuthorizeData[] authorization = endpoint.Metadata.OfType<IAuthorizeData>().ToArray();
            if (oidcEnabled)
            {
                authorization.ShouldHaveSingleItem().Policy.ShouldBe(
                    TenantsFrontComposerRegistration.GlobalAdministratorPolicy);
            }
            else
            {
                authorization.ShouldBeEmpty(
                    "without an authentication scheme, endpoint authorization would challenge through a missing service instead of rendering fail-closed");
            }
        }
    }

    /// <summary>
    /// Complements the resolved dependency-closure test with compiled-artifact assertions: the REST
    /// generator is opt-in on an assembly-level <c>RestApi</c> attribute, and the referenced-assembly
    /// set proves which client and contract seams the compiled UI actually binds to.
    /// </summary>
    [Fact]
    public void TenantsUiAssembly_BindsTypedClientSeamAndCarriesNoRestApiOptIn()
    {
        Assembly uiAssembly = typeof(TenantsUiServiceCollectionExtensions).Assembly;
        string[] referenced = uiAssembly
            .GetReferencedAssemblies()
            .Select(static name => name.Name ?? string.Empty)
            .ToArray();

        referenced.ShouldNotBeEmpty("Referenced-assembly metadata is required for the assertions below.");
        referenced.ShouldContain(
            "Hexalith.EventStore.Client",
            "Interactive Tenants UI must bind the EventStore typed query client; this is the seam AC1 "
            + "requires and it reaches the UI transitively, so only the compiled assembly can prove it.");
        referenced.ShouldContain(
            "Hexalith.Tenants.Contracts",
            "Interactive Tenants UI must bind the shared Tenants contracts through the typed client seam.");

        foreach (string forbidden in new[] { "Hexalith.Tenants.Api", "Hexalith.Tenants", "Hexalith.EventStore.RestApi.Generators" })
        {
            referenced.ShouldNotContain(
                forbidden,
                $"Interactive Tenants UI must not directly bind {forbidden}; the resolved MSBuild closure test covers transitive acquisition.");
        }

        // "Components never call Memories" was an unenforced claim. Hexalith.Memories.Client.Rest is
        // necessarily referenced -- the gateway uses it -- so the assembly-level ban above cannot express
        // this. Adding @inject MemoriesClient to a razor component would have been caught by nothing.
        Type[] componentTypes = uiAssembly.GetTypes()
            .Where(static type => typeof(Microsoft.AspNetCore.Components.IComponent).IsAssignableFrom(type))
            .ToArray();
        componentTypes.ShouldNotBeEmpty("the component scan must observe real components to mean anything");

        foreach (Type componentType in componentTypes)
        {
            IEnumerable<Type> injectedTypes = componentType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(static property => property.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.InjectAttribute), inherit: true).Length > 0)
                .Select(static property => property.PropertyType)
                .Concat(componentType
                    .GetConstructors()
                    .SelectMany(static constructor => constructor.GetParameters())
                    .Select(static parameter => parameter.ParameterType));

            foreach (Type injected in injectedTypes)
            {
                (injected.FullName ?? string.Empty).StartsWith("Hexalith.Memories", StringComparison.Ordinal)
                    .ShouldBeFalse(
                        $"{componentType.FullName} must reach Memories through the server-side gateway, never directly.");
            }
        }

        // The declared-injection scan above is necessary but not sufficient. A component could resolve a
        // neutral wrapper whose implementation lived outside Components, or use a type alias that hid the
        // Memories token from the component file. Scan the whole production project and permit the client
        // seam only in the gateway implementation and its composition root. That closes both routes: an
        // alias must name the forbidden namespace somewhere, and a wrapper must acquire Memories somewhere.
        string uiProjectRoot = UiProjectRoot();
        string[] productionSources = Directory
            .EnumerateFiles(uiProjectRoot, "*.*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".razor", StringComparison.Ordinal)
                || path.EndsWith(".cs", StringComparison.Ordinal))
            .Where(path => !Path.GetRelativePath(uiProjectRoot, path)
                .StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !Path.GetRelativePath(uiProjectRoot, path)
                    .StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .ToArray();
        productionSources.Length.ShouldBeGreaterThan(20, "the source scan must observe the full UI project");

        HashSet<string> allowedMemoriesSources = new(StringComparer.Ordinal)
        {
            Path.Combine("Extensions", "TenantsUiServiceCollectionExtensions.cs"),
            Path.Combine("Services", "Gateways", "TenantQueryGateway.cs"),
        };
        List<string> unauthorizedMemoriesSources = [];
        foreach (string path in productionSources)
        {
            string source = File.ReadAllText(path);
            bool acquiresMemories = source.Contains("Hexalith.Memories", StringComparison.Ordinal)
                || Regex.IsMatch(source, @"\b(?:MemoriesClient|AddMemoriesClient)\b", RegexOptions.CultureInvariant);
            string relative = Path.GetRelativePath(uiProjectRoot, path);
            if (acquiresMemories && !allowedMemoriesSources.Contains(relative))
            {
                unauthorizedMemoriesSources.Add(relative);
            }
        }

        unauthorizedMemoriesSources.ShouldBeEmpty(
            "Only TenantQueryGateway and the composition root may acquire Memories. Components and neutral "
            + "wrappers must reach search through ITenantQueryGateway. Offending files: "
            + string.Join(", ", unauthorizedMemoriesSources));

        uiAssembly.GetCustomAttributesData()
            .Where(static attribute => string.Equals(
                attribute.AttributeType.FullName,
                "Hexalith.EventStore.Contracts.Rest.RestApiAttribute",
                StringComparison.Ordinal))
            .ShouldBeEmpty(
                "Interactive Tenants UI must not declare the generated-REST assembly opt-in. The generator "
                + "emits controllers only for assemblies carrying this attribute, so its absence keeps the "
                + "generator inert even if it were acquired transitively.");
    }

    [Fact]
    public void TenantsUiAssembly_DoesNotContainMvcControllers()
    {
        var manager = new ApplicationPartManager();
        manager.ApplicationParts.Add(new AssemblyPart(typeof(TenantsUiServiceCollectionExtensions).Assembly));
        manager.FeatureProviders.Add(new ControllerFeatureProvider());
        var controllers = new ControllerFeature();
        manager.PopulateFeature(controllers);

        controllers.Controllers.ShouldBeEmpty(
            "Interactive Tenants UI must not compile any MVC-discoverable controller, including POCO types marked [Controller].");
    }

    [Fact]
    public async Task TenantsUiHost_DoesNotExposeTenantManagementApiEndpoints()
    {
        await using var factory = new WebApplicationFactory<global::Program>();
        Endpoint[] endpoints = factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(static source => source.Endpoints)
            .ToArray();

        // Positive control. Without it the negative assertion below would pass for the wrong reason
        // if the reduced test host ever stopped producing endpoints at all.
        endpoints.ShouldNotBeEmpty(
            "The Tenants UI test host produced no endpoints, so the forbidden-endpoint assertion "
            + "below would pass vacuously rather than because the host is clean.");

        string[] forbiddenEndpoints = endpoints
            .Where(static endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not null
                || endpoint is RouteEndpoint route && IsTenantManagementApiRoute(route.RoutePattern.RawText))
            .Select(static endpoint => endpoint.DisplayName ?? "<unnamed endpoint>")
            .ToArray();

        forbiddenEndpoints.ShouldBeEmpty(
            "Interactive Tenants UI must not expose MVC controllers or tenant-management API routes.");
    }

    [Theory]
    [InlineData("api/tenants")]
    [InlineData("api/v1/tenants/{tenantId}")]
    [InlineData("api/v1.0/tenants/{tenantId}")]
    [InlineData("api/v{version:apiVersion}/users/{userId}/tenants")]
    [InlineData("tenant-gateway/api/v12/users/{userId}/tenants")]
    [InlineData("{deploymentPrefix}/api/global-administrators")]
    public void Tenant_management_route_detection_is_prefix_and_version_independent(string routePattern)
    {
        IsTenantManagementApiRoute(routePattern).ShouldBeTrue();
    }

    [Fact]
    public void Localization_resources_resolve_english_and_french_workspace_copy()
    {
        ResourceManager manager = new(typeof(TenantsResources));

        manager.GetString("Tenants.Workspace.UnavailableHeading", CultureInfo.InvariantCulture)
            .ShouldBe("Tenant read surfaces are not connected yet");
        manager.GetString("Tenants.Workspace.UnavailableHeading", CultureInfo.GetCultureInfo("fr"))
            .ShouldBe("Les surfaces de lecture des locataires ne sont pas encore connectées");
        manager.GetString("Tenants.UserLookup.Title", CultureInfo.InvariantCulture)
            .ShouldBe("User membership lookup");
        manager.GetString("Tenants.UserLookup.Title", CultureInfo.GetCultureInfo("fr"))
            .ShouldBe("Recherche des appartenances utilisateur");
        manager.GetString("Tenants.Create.State.ProjectionPending", CultureInfo.InvariantCulture)
            .ShouldBe("Projection pending; tenant is not confirmed visible yet.");
        manager.GetString("Tenants.Create.State.ProjectionPending", CultureInfo.GetCultureInfo("fr"))
            .ShouldBe("Projection en attente ; le locataire n'est pas encore confirmé visible.");
        manager.GetString("Tenants.RemoveMember.State.ProjectionPending", CultureInfo.InvariantCulture)
            .ShouldBe("Projection pending; the target user is not confirmed absent yet.");
        manager.GetString("Tenants.RemoveMember.State.ProjectionPending", CultureInfo.GetCultureInfo("fr"))
            .ShouldBe("Projection en attente ; l'utilisateur cible n'est pas encore confirmé absent.");
        manager.GetString("Tenants.GlobalAdministrators.Title", CultureInfo.InvariantCulture)
            .ShouldBe("Global Administrators");
        manager.GetString("Tenants.GlobalAdministrators.Title", CultureInfo.GetCultureInfo("fr"))
            .ShouldBe("Administrateurs globaux");
        manager.GetString("Tenants.GlobalAdministrators.State.Stale.Title", CultureInfo.InvariantCulture)
            .ShouldBe("Global administrator data stale");
        manager.GetString("Tenants.GlobalAdministrators.State.Stale.Title", CultureInfo.GetCultureInfo("fr"))
            .ShouldBe("Donnees d'administrateurs globaux perimees");
        manager.GetString("Tenants.GlobalAdministrators.State.Ready.Title", CultureInfo.InvariantCulture)
            .ShouldBe("Global administrators loaded");
        manager.GetString("Tenants.GlobalAdministrators.State.Ready.Title", CultureInfo.GetCultureInfo("fr"))
            .ShouldBe("Administrateurs globaux charges");
        manager.GetString("Tenants.GlobalAdministrators.State.Unauthorized.Title", CultureInfo.InvariantCulture)
            .ShouldBe("Platform area unavailable");
        manager.GetString("Tenants.GlobalAdministrators.State.Unauthorized.Title", CultureInfo.GetCultureInfo("fr"))
            .ShouldBe("Zone plateforme indisponible");
    }

    [Fact]
    public void Clipboard_feedback_is_complete_polite_recovery_copy_in_english_and_french()
    {
        ResourceManager manager = new(typeof(TenantsResources));
        Dictionary<string, (string English, string French)> expected = new(StringComparer.Ordinal)
        {
            ["Tenants.Copy.Feedback.Copied"] = ("Copied.", "Copié."),
            ["Tenants.Copy.Feedback.Canceled"] = (
                "Copy could not be completed. Select the value and copy it manually.",
                "La copie n'a pas pu être effectuée. Sélectionnez la valeur et copiez-la manuellement."),
            ["Tenants.Copy.Feedback.Disconnected"] = (
                "Clipboard disconnected. Copy was not completed. Select the value and copy it manually.",
                "Presse-papiers déconnecté. La copie n'a pas été effectuée. Sélectionnez la valeur et copiez-la manuellement."),
            ["Tenants.Copy.Feedback.Failed"] = (
                "Copy failed. Select the value and copy it manually.",
                "La copie a échoué. Sélectionnez la valeur et copiez-la manuellement."),
            ["Tenants.Copy.Feedback.Insecure"] = (
                "Clipboard is unavailable in this browser context. Select the value and copy it manually.",
                "Le presse-papiers est indisponible dans ce contexte de navigateur. Sélectionnez la valeur et copiez-la manuellement."),
            ["Tenants.Copy.Feedback.PermissionDenied"] = (
                "Clipboard permission was not granted. Select the value and copy it manually.",
                "L'autorisation du presse-papiers n'a pas été accordée. Sélectionnez la valeur et copiez-la manuellement."),
            ["Tenants.Copy.Feedback.Unavailable"] = (
                "Clipboard unavailable. Select the value and copy it manually.",
                "Presse-papiers indisponible. Sélectionnez la valeur et copiez-la manuellement."),
        };

        foreach ((string key, (string english, string french)) in expected)
        {
            manager.GetString(key, CultureInfo.InvariantCulture).ShouldBe(english);
            manager.GetString(key, CultureInfo.GetCultureInfo("fr")).ShouldBe(french);
        }
    }

    [Fact]
    public void Every_shared_copy_consumer_declares_explicit_approval_and_configuration_fails_closed()
    {
        string componentsRoot = Path.Combine(ProjectRoot(), "src", "Hexalith.Tenants.UI", "Components");
        string[] razorFiles = Directory.GetFiles(componentsRoot, "*.razor", SearchOption.AllDirectories);
        List<string> implicitConsumers = [];
        int consumerCount = 0;
        int inspectedConsumerCount = 0;

        foreach (string file in razorFiles)
        {
            string source = File.ReadAllText(file);
            consumerCount += Regex.Matches(
                source,
                "<SupportSafeCopyButton\\b",
                RegexOptions.CultureInvariant).Count;
            foreach (Match match in Regex.Matches(
                source,
                "<SupportSafeCopyButton\\b(?<attributes>[^>]*)>",
                RegexOptions.CultureInvariant | RegexOptions.Singleline))
            {
                inspectedConsumerCount++;
                if (!match.Groups["attributes"].Value.Contains("IsApproved=\"true\"", StringComparison.Ordinal))
                {
                    implicitConsumers.Add(Path.GetRelativePath(componentsRoot, file));
                }
            }
        }

        implicitConsumers.ShouldBeEmpty(
            $"Every shared copy control must receive explicit outer-surface approval: {string.Join(", ", implicitConsumers)}");
        inspectedConsumerCount.ShouldBe(consumerCount, "Every opening copy-control tag must be inspected, including paired tags.");

        string configuration = File.ReadAllText(Path.Combine(
            componentsRoot,
            "Tenants",
            "TenantConfigurationView.razor"));
        configuration.ShouldNotContain("<SupportSafeCopyButton");
        configuration.ShouldNotContain("tenants-config-copy-reference");
    }

    [Fact]
    public void Story_1_8_evidence_tracks_closed_safe_model_separately_from_open_external_blockers()
    {
        string report = File.ReadAllText(Path.Combine(
            ProjectRoot(),
            "_bmad-output",
            "implementation-artifacts",
            "story-1-8-support-safe-identifier-copy-and-read-experience-evidence-2026-07-21.md"));

        string configurationRecord = EvidenceRecord(report, "CFG-1.6-SAFE-MODEL");
        configurationRecord.ShouldContain("owner:");
        configurationRecord.ShouldContain("CLOSED 2026-07-22");
        configurationRecord.ShouldContain("clipboard activation and certification remain intentionally absent");

        foreach (string blocker in new[] { "BROWSER-COPY-1.8", "AT-NVDA-1.8" })
        {
            string blockerRecord = EvidenceRecord(report, blocker);
            blockerRecord.ShouldContain("owner:");
            blockerRecord.ShouldContain("Consequence:");
            blockerRecord.ShouldContain("Reopen trigger:");
        }
    }

    private static string EvidenceRecord(string report, string recordId)
    {
        int recordStart = report.IndexOf(recordId, StringComparison.Ordinal);
        recordStart.ShouldBeGreaterThanOrEqualTo(0);
        string record = report[recordStart..];
        int nextRecord = record.IndexOf("\n- **", 1, StringComparison.Ordinal);
        return nextRecord >= 0 ? record[..nextRecord] : record;
    }

    [Fact]
    public void Global_administrators_and_navigation_resources_have_english_french_key_parity()
    {
        string resourceRoot = Path.Combine(ProjectRoot(), "src", "Hexalith.Tenants.UI", "Resources");
        string[] prefixes = ["Tenants.GlobalAdministrators.", "Tenants.Navigation."];

        HashSet<string> englishKeys = ReadResourceKeys(Path.Combine(resourceRoot, "TenantsResources.resx"), prefixes);
        HashSet<string> frenchKeys = ReadResourceKeys(Path.Combine(resourceRoot, "TenantsResources.fr.resx"), prefixes);

        englishKeys.ShouldBe(frenchKeys);
    }

    [Fact]
    public void All_english_and_french_resources_have_key_parity()
    {
        string resourceRoot = Path.Combine(ProjectRoot(), "src", "Hexalith.Tenants.UI", "Resources");
        HashSet<string> englishKeys = ReadAllResourceKeys(Path.Combine(resourceRoot, "TenantsResources.resx"));
        HashSet<string> frenchKeys = ReadAllResourceKeys(Path.Combine(resourceRoot, "TenantsResources.fr.resx"));

        englishKeys.ShouldBe(frenchKeys);
    }

    [Fact]
    public void Audit_availability_resources_have_english_french_key_parity_and_no_machine_tokens()
    {
        string resourceRoot = Path.Combine(ProjectRoot(), "src", "Hexalith.Tenants.UI", "Resources");
        string[] prefixes = ["Tenants.Audit.Availability."];

        string englishPath = Path.Combine(resourceRoot, "TenantsResources.resx");
        string frenchPath = Path.Combine(resourceRoot, "TenantsResources.fr.resx");
        HashSet<string> englishKeys = ReadResourceKeys(englishPath, prefixes);
        HashSet<string> frenchKeys = ReadResourceKeys(frenchPath, prefixes);
        string englishAvailabilityValues = string.Join('\n', ReadResourceValues(englishPath, prefixes));
        string frenchAvailabilityValues = string.Join('\n', ReadResourceValues(frenchPath, prefixes));

        englishKeys.ShouldBe(frenchKeys);
        englishKeys.ShouldContain("Tenants.Audit.Availability.State.Pending");
        englishKeys.ShouldContain("Tenants.Audit.Availability.Action.ContinueReadOnly");
        englishAvailabilityValues.ShouldNotContain("AuditPending", Case.Insensitive);
        englishAvailabilityValues.ShouldNotContain("audit_pending", Case.Insensitive);
        frenchAvailabilityValues.ShouldNotContain("AuditPending", Case.Insensitive);
        frenchAvailabilityValues.ShouldNotContain("audit_pending", Case.Insensitive);
    }

    [Theory]
    [InlineData("TenantsResources.resx")]
    [InlineData("TenantsResources.fr.resx")]
    public void Command_audit_entry_point_visible_label_is_contained_in_its_accessible_name(string resourceFile)
    {
        // WCAG 2.5.3 label in name: the command entry point shows the canonical Inspect-audit verb, and its
        // accessible name starts with that exact label, including the apostrophe code point.
        Dictionary<string, string> resources = ReadResourceMap(
            Path.Combine(ProjectRoot(), "src", "Hexalith.Tenants.UI", "Resources", resourceFile));
        string label = resources["Tenants.Audit.Availability.Action.InspectAudit"];
        string accessibleName = resources["Tenants.Audit.EntryPoint.Accessible.Command"];

        accessibleName.ShouldStartWith(label, Case.Sensitive);
        label.ShouldNotContain("'", Case.Sensitive);
    }

    [Fact]
    public void Audit_availability_copy_is_one_shared_accented_set_whose_explanations_name_no_recovery()
    {
        string resourceRoot = Path.Combine(ProjectRoot(), "src", "Hexalith.Tenants.UI", "Resources");
        string englishPath = Path.Combine(resourceRoot, "TenantsResources.resx");
        string frenchPath = Path.Combine(resourceRoot, "TenantsResources.fr.resx");
        Dictionary<string, string> english = ReadResourceMap(englishPath);
        Dictionary<string, string> french = ReadResourceMap(frenchPath);
        string[] required =
        [
            "Tenants.Audit.Availability.State.Pending",
            "Tenants.Audit.Availability.State.Delayed",
            "Tenants.Audit.Availability.State.Unavailable",
            "Tenants.Audit.Availability.State.Available",
            "Tenants.Audit.Availability.State.MissingSupport",
            "Tenants.Audit.Availability.Reason.Pending",
            "Tenants.Audit.Availability.Reason.Delayed",
            "Tenants.Audit.Availability.Reason.Unavailable",
            "Tenants.Audit.Availability.Reason.MissingSupport",
            "Tenants.Audit.Availability.Action.Refresh",
            "Tenants.Audit.Availability.Action.InspectAudit",
            "Tenants.Audit.Availability.Action.ContinueReadOnly",
            "Tenants.Audit.Recovery.Action.Escalate",
            "Tenants.Audit.Availability.ActionsLabel",
            "Tenants.Audit.Availability.RetryLimit",
            "Tenants.Audit.Receipt.Availability.Unavailable.Reason",
        ];

        foreach (string key in required)
        {
            english.ShouldContainKey(key);
            french.ShouldContainKey(key);
            english[key].ShouldNotBeNullOrWhiteSpace();
            french[key].ShouldNotBeNullOrWhiteSpace();
        }

        // One shared control, one shared vocabulary: no recovery-copy variants and no flow-local duplicates.
        string[] flowLocalPrefixes =
        [
            "Tenants.Create.Audit.", "Tenants.AddMember.Audit.", "Tenants.ChangeRole.Audit.", "Tenants.RemoveMember.Audit.",
            "Tenants.EditMetadata.Audit.", "Tenants.Lifecycle.Audit.", "Tenants.Configuration.Set.Audit.",
            "Tenants.Configuration.Remove.Audit.",
        ];
        foreach (string key in english.Keys.Concat(french.Keys))
        {
            key.ShouldNotStartWith("Tenants.Audit.Availability.Accessible.");
            key.ShouldNotEndWith(".NoEscalation");
            key.ShouldNotEndWith(".NoRecovery");
            key.ShouldNotEndWith(".RefreshOnly");
            flowLocalPrefixes.ShouldNotContain(prefix => key.StartsWith(prefix, StringComparison.Ordinal), key);
        }

        english.ShouldNotContainKey("Tenants.Audit.Availability.Action.Wait");
        english.ShouldNotContainKey("Tenants.Audit.Availability.Action.Escalate");
        french.ShouldNotContainKey("Tenants.Audit.Availability.Action.Escalate");
        english.ShouldNotContainKey("Tenants.Audit.Receipt.Availability.Unavailable.Accessible");

        // Explanations describe the state; only the rendered controls carry the recovery.
        string[] explanationKeys =
        [
            "Tenants.Audit.Availability.Reason.Pending",
            "Tenants.Audit.Availability.Reason.Delayed",
            "Tenants.Audit.Availability.Reason.Unavailable",
            "Tenants.Audit.Availability.Reason.MissingSupport",
            "Tenants.Audit.Receipt.Availability.Unavailable.Reason",
        ];
        string[] englishVerbs = ["wait", "refresh", "retry", "inspect", "continue", "read-only", "escalat", "request permission"];
        string[] frenchVerbs = ["attend", "actualis", "réessa", "reessa", "relanc", "inspect", "continu", "lecture seule", "escalad", "demander"];
        foreach (string key in explanationKeys)
        {
            foreach (string verb in englishVerbs)
            {
                english[key].ShouldNotContain(verb, Case.Insensitive, $"{key} names a recovery");
            }

            foreach (string verb in frenchVerbs)
            {
                french[key].ShouldNotContain(verb, Case.Insensitive, $"{key} (fr) names a recovery");
            }

            english[key].ShouldNotContain("success", Case.Insensitive);
        }

        english["Tenants.Audit.Availability.Reason.MissingSupport"].ShouldContain("in-panel audit verification", Case.Insensitive);

        // Delayed is reached from status timeouts, publish failures, retention expiry, and proof-read delays, so
        // its explanation names no single cause.
        foreach (string cause in new[] { "timed out", "timeout", "publication", "publish", "tracking window" })
        {
            english["Tenants.Audit.Availability.Reason.Delayed"].ShouldNotContain(cause, Case.Insensitive);
        }

        foreach (string cause in new[] { "expiré", "publication", "fenêtre de suivi" })
        {
            french["Tenants.Audit.Availability.Reason.Delayed"].ShouldNotContain(cause, Case.Insensitive);
        }
        french["Tenants.Audit.Availability.Reason.MissingSupport"].ShouldContain("vérification d’audit intégrée au panneau");

        // The French availability set is written with its accents, never an ASCII-folded approximation.
        french["Tenants.Audit.Availability.State.Delayed"].ShouldBe("Audit retardé");
        french["Tenants.Audit.Availability.State.MissingSupport"].ShouldBe("Support d’implémentation manquant");
        // The shared set also includes required keys outside that prefix: the receipt's unavailable reason and Escalate.
        string frenchAvailability = string.Join(
            '\n',
            ReadResourceValues(frenchPath, ["Tenants.Audit.Availability."]).Concat(required.Select(key => french[key])));
        foreach (string folded in new[] { "retarde ", "implementation", "verifier", "verifiee", "reessayez", "disponibilite", "evenement", "resultat" })
        {
            frenchAvailability.ShouldNotContain(folded, Case.Insensitive);
        }
    }

    [Fact]
    public void Main_layout_composes_body_through_frontcomposer_shell()
    {
        string layout = File.ReadAllText(
            Path.Combine(ProjectRoot(), "src", "Hexalith.Tenants.UI", "Components", "Layout", "MainLayout.razor"));

        // The shell title is configured through FcShellOptions so deployments can override it
        // without changing layout markup.
        layout.ShouldContain("<FrontComposerShell>@Body</FrontComposerShell>");
        layout.ShouldNotContain("AppTitle=");
        layout.ShouldContain("@Body");

        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(ProjectRoot(), "src", "Hexalith.Tenants.UI", "appsettings.json")));
        JsonElement shellSettings = settings.RootElement.GetProperty("Hexalith").GetProperty("Shell");
        shellSettings.GetProperty("AppTitle").GetString().ShouldBe("Hexalith Tenants");

        // Sign in / out is now the framework header's FcAccountMenu (avatar) — the bespoke
        // content-area auth bar has been removed.
        layout.ShouldNotContain("tenants-auth-bar");
        // The left navigation is framework-owned now — no bespoke navigation slot/component.
        layout.ShouldNotContain("<Navigation>");
        layout.ShouldNotContain("OperationsShellNavigation");
    }

    [Fact]
    public void Document_language_uses_the_active_request_culture()
    {
        string app = File.ReadAllText(
            Path.Combine(ProjectRoot(), "src", "Hexalith.Tenants.UI", "Components", "App.razor"));

        // Document language is driven by the active request culture (not a hardcoded value)...
        app.ShouldContain("CultureInfo.CurrentUICulture.TwoLetterISOLanguageName");
        // ...but clamped to a supported tag so the invariant culture cannot emit an invalid lang="iv".
        app.ShouldContain("? \"fr\" : \"en\"");
        app.ShouldNotContain("<html lang=\"en\">");
        app.ShouldNotContain("lang=\"iv\"");
    }

    [Fact]
    public void Release_workflow_does_not_claim_an_unsupported_tenants_ui_container_handoff()
    {
        string workflow = File.ReadAllText(Path.Combine(ProjectRoot(), ".github/workflows/release.yml"));

        workflow.ShouldNotContain("src/Hexalith.Tenants.UI/Hexalith.Tenants.UI.csproj|tenants-ui");
    }

    [Fact]
    public void Styles_include_forced_colors_and_visible_focus_rules()
    {
        // 2026-06-25 ergonomic pass: TenantsWorkspace no longer ships component CSS. Its surface is
        // composed entirely from Fluent v5 primitives + FrontComposer chrome, which own their own focus
        // and forced-colors affordances; the prior workspace stylesheet only styled status/focus-link
        // classes that the markup no longer renders. The route <h1> focus ring is owned by FrontComposer
        // (FcPageHeader.razor.css). Pages that still hand-author bespoke surfaces keep their a11y CSS
        // pinned below.
        string globalAdminStyles = File.ReadAllText(
            Path.Combine(ProjectRoot(), "src", "Hexalith.Tenants.UI", "Components", "Pages", "GlobalAdministratorsPage.razor.css"));

        globalAdminStyles.ShouldContain("@media (forced-colors: active)");
        globalAdminStyles.ShouldContain(":focus-visible");
        globalAdminStyles.ShouldContain("outline");
    }

    /// <summary>The typed-client name used by <see cref="MemoriesClient"/> HTTP factory options.</summary>
    private const string MemoriesHttpClientName = nameof(MemoriesClient);

    /// <summary>A sibling client that keeps default logging so the capture is proven able to observe it.</summary>
    private const string ControlHttpClientName = "tenants-ui-tests-control-client";

    /// <summary>
    /// A URL whose query string carries exactly the raw search material that must never be logged. The
    /// framework already redacts query values in its default request logs, so the channel this closes is the
    /// remaining per-request record (path, timing, outcome) for the Memories client.
    /// </summary>
    private const string RawSearchUrl = "https://memories.invalid/v1/search?query=needle&offset=40";

    private static async Task StartConfigurationDiagnosticsReporterAsync(IServiceProvider provider)
    {
        IHostedService reporter = provider.GetServices<IHostedService>()
            .Single(static service => service is TenantsUiConfigurationDiagnosticsReporter);
        await reporter.StartAsync(CancellationToken.None);
    }

    private static void AddStubPrimaryHandler(IServiceCollection services, string clientName)
        => services.AddHttpClient(clientName)
            .ConfigurePrimaryHttpMessageHandler(static () => new StubPrimaryHandler());

    private static async Task AssertMemoriesHttpLoggingIsSuppressedAsync(
        IServiceProvider provider,
        CapturingLoggerProvider capture)
    {
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();

        // Control: the same capture does observe default HttpClient request logging for a sibling client
        // that keeps its loggers. Without this the suppression assertion below could never fail.
        using (HttpClient control = factory.CreateClient(ControlHttpClientName))
        {
            using HttpResponseMessage controlResponse = await control.GetAsync(new Uri(RawSearchUrl));
            controlResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        capture.Messages.ShouldContain(static message => message.Contains("/v1/search", StringComparison.Ordinal));
        capture.Clear();

        using HttpClient memories = factory.CreateClient(MemoriesHttpClientName);
        using HttpResponseMessage response = await memories.GetAsync(new Uri(RawSearchUrl));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        capture.Messages.ShouldBeEmpty();
    }

    private sealed class StubPrimaryHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    private sealed class PrimaryHandlerCapture : IHttpMessageHandlerBuilderFilter
    {
        public HttpMessageHandler? PrimaryHandler { get; private set; }

        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
        {
            ArgumentNullException.ThrowIfNull(next);
            return builder =>
            {
                next(builder);
                PrimaryHandler = builder.PrimaryHandler;
            };
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _messages = new();
        private readonly bool _captureAll;

        public CapturingLoggerProvider(bool captureAll = false)
        {
            _captureAll = captureAll;
        }

        public IReadOnlyCollection<string> Messages => [.. _messages];

        public void Clear() => _messages.Clear();

        public ILogger CreateLogger(string categoryName)
            => _captureAll || categoryName.StartsWith("System.Net.Http.HttpClient.", StringComparison.Ordinal)
                ? new CapturingLogger(_messages, categoryName)
                : NullLogger.Instance;

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(
            System.Collections.Concurrent.ConcurrentQueue<string> messages,
            string categoryName) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                ArgumentNullException.ThrowIfNull(formatter);
                messages.Enqueue($"{categoryName}: {formatter(state, exception)}");
            }
        }
    }

    private static string ProjectRoot()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static async Task<string[]> ReadResolvedDependencyValuesAsync(
        string projectPath,
        bool useProjectReferences)
    {
        string dependencyMode = useProjectReferences ? "source" : "package";
        string isolatedIntermediatePath = Path.Combine("obj", "dependency-resolution", dependencyMode);
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = ProjectRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("restore");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--force-evaluate");
        startInfo.ArgumentList.Add("-nodeReuse:false");

        // NuGet's assets file is the resolved project/package closure. It includes transitive packages
        // (including analyzers when they actually flow to the UI) and transitive project libraries,
        // without compiling shared outputs or relying on direct evaluation-only items.
        startInfo.ArgumentList.Add($"-property:UseHexalithProjectReferences={useProjectReferences.ToString().ToLowerInvariant()}");
        startInfo.ArgumentList.Add($"-property:Configuration={(useProjectReferences ? "Debug" : "Release")}");
        startInfo.ArgumentList.Add(
            $"-property:BaseIntermediateOutputPath={isolatedIntermediatePath}{Path.DirectorySeparatorChar}");
        startInfo.ArgumentList.Add("-property:HexalithMemoriesFromSource=false");
        startInfo.ArgumentList.Add("-property:HexalithCommonsFromSource=false");

        Process? started;
        try
        {
            started = Process.Start(startInfo);
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                "Could not launch 'dotnet' for UI dependency resolution. The .NET SDK must be on PATH "
                + "for this test to have any authority; a missing SDK must fail loudly rather than "
                + "silently skip the generated-REST governance assertions.",
                ex);
        }

        using Process process = started
            ?? throw new InvalidOperationException("Could not start dotnet restore for UI dependency resolution.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(DependencyResolutionTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The process exited between the timeout firing and the kill request.
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            _ = await standardOutput.ConfigureAwait(false);
            _ = await standardError.ConfigureAwait(false);

            throw new TimeoutException(
                $"dotnet restore dependency resolution for '{projectPath}' did not exit within "
                + $"{DependencyResolutionTimeout.TotalSeconds:F0}s and was terminated.");
        }

        string output = await standardOutput.ConfigureAwait(false);
        string error = await standardError.ConfigureAwait(false);

        process.ExitCode.ShouldBe(0, $"dotnet restore dependency resolution failed: {error}{Environment.NewLine}{output}");

        string assetsPath = Path.Combine(
            Path.GetDirectoryName(projectPath)
                ?? throw new InvalidOperationException($"Project path '{projectPath}' has no parent directory."),
            isolatedIntermediatePath,
            "project.assets.json");
        File.Exists(assetsPath).ShouldBeTrue(
            $"dotnet restore succeeded but did not produce the expected assets closure at '{assetsPath}'.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(await File.ReadAllTextAsync(assetsPath).ConfigureAwait(false));
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"NuGet assets file '{assetsPath}' is not valid JSON, so the UI dependency closure could not be inspected.",
                ex);
        }

        using (document)
        {
            return ReadDependencyValues(document);
        }
    }

    private static string[] ReadDependencyValues(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("libraries", out JsonElement libraries)
            || libraries.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "NuGet assets output contained no 'libraries' object. The resolution contract changed, "
                + "and every dependency assertion built on it would pass vacuously.");
        }

        var values = new List<string>();
        foreach (JsonProperty library in libraries.EnumerateObject())
        {
            int versionSeparator = library.Name.IndexOf('/', StringComparison.Ordinal);
            values.Add(versionSeparator < 0 ? library.Name : library.Name[..versionSeparator]);
        }

        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool MatchesDependencyIdentity(string value, string expectedIdentity)
    {
        string normalized = value.Replace('\\', '/').Trim();
        return string.Equals(normalized, expectedIdentity, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith($"{expectedIdentity},", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith($"/{expectedIdentity}", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith($"/{expectedIdentity}.csproj", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith($"/{expectedIdentity}.dll", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTenantManagementApiRoute(string? routePattern)
    {
        string[] segments = (routePattern ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (int index = 0; index < segments.Length; index++)
        {
            if (!string.Equals(segments[index], "api", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int resourceIndex = index + 1;
            if (resourceIndex < segments.Length && IsApiVersionSegment(segments[resourceIndex]))
            {
                resourceIndex++;
            }

            if (resourceIndex < segments.Length
                && segments[resourceIndex] is string resource
                && (resource.Equals("tenants", StringComparison.OrdinalIgnoreCase)
                    || resource.Equals("users", StringComparison.OrdinalIgnoreCase)
                    || resource.Equals("global-administrators", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsApiVersionSegment(string segment)
    {
        if (segment.Length <= 1 || segment[0] is not ('v' or 'V'))
        {
            return false;
        }

        ReadOnlySpan<char> version = segment.AsSpan(1);
        if (version[0] == '{' && version[^1] == '}')
        {
            ReadOnlySpan<char> parameter = version[1..^1];
            int constraintSeparator = parameter.IndexOf(':');
            ReadOnlySpan<char> name = constraintSeparator < 0 ? parameter : parameter[..constraintSeparator];
            ReadOnlySpan<char> constraint = constraintSeparator < 0 ? [] : parameter[(constraintSeparator + 1)..];
            return name.Equals("version", StringComparison.OrdinalIgnoreCase)
                && (constraint.IsEmpty || constraint.Equals("apiVersion", StringComparison.OrdinalIgnoreCase));
        }

        bool expectsDigit = true;
        foreach (char character in version)
        {
            if (char.IsDigit(character))
            {
                expectsDigit = false;
                continue;
            }

            if (character != '.' || expectsDigit)
            {
                return false;
            }

            expectsDigit = true;
        }

        return !expectsDigit;
    }

    private static HashSet<string> ReadResourceKeys(string path, string[] prefixes)
        => XDocument
            .Load(path)
            .Descendants("data")
            .Select(static element => element.Attribute("name")?.Value)
            .Where(name => name is not null && prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> ReadAllResourceKeys(string path)
        => XDocument
            .Load(path)
            .Descendants("data")
            .Select(static element => element.Attribute("name")?.Value)
            .Where(static name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, string> ReadResourceMap(string path)
        => XDocument
            .Load(path)
            .Descendants("data")
            .Where(static element => element.Attribute("name") is not null)
            .ToDictionary(
                static element => element.Attribute("name")!.Value,
                static element => element.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);

    private static IEnumerable<string> ReadResourceValues(string path, string[] prefixes)
        => XDocument
            .Load(path)
            .Descendants("data")
            .Where(element =>
            {
                string? name = element.Attribute("name")?.Value;
                return name is not null && prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
            })
            .Select(static element => element.Element("value")?.Value)
            .Where(static value => value is not null)
            .Select(static value => value!);

    private static IHttpContextAccessor ContextAccessor(params Claim[] claims)
        => new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = Principal(claims),
            },
        };

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => Principal(authenticated: true, claims);

    private static ClaimsPrincipal Principal(bool authenticated, params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticated ? "test" : null));

    private sealed class CapturingRegistry : IFrontComposerRegistry, IFrontComposerNavEntryRegistry
    {
        public List<(string Name, string BoundedContext)> NavGroups { get; } = [];

        public List<FrontComposerNavEntry> NavEntries { get; } = [];

        public List<DomainManifest> Manifests { get; } = [];

        public void AddNavGroup(string name, string boundedContext)
            => NavGroups.Add((name, boundedContext));

        public void AddNavEntry(FrontComposerNavEntry entry)
            => NavEntries.Add(entry);

        public IReadOnlyList<FrontComposerNavEntry> GetNavEntries()
            => NavEntries;

        public IReadOnlyList<DomainManifest> GetManifests()
            => Manifests;

        public void RegisterDomain(DomainManifest manifest)
            => Manifests.Add(manifest);
    }

    private sealed class AuthorizationRecordingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Authorization = request.Headers.Authorization?.ToString();
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"items\":[],\"cursor\":null,\"hasMore\":false}"),
            });
        }
    }

    private sealed class StubTenantCommandGateway : ITenantCommandGateway
    {
        public Task<TenantCommandSubmissionResult> CreateTenantAsync(CreateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Accepted("message-1", "correlation-1"));

        public Task<TenantCommandSubmissionResult> AddUserToTenantAsync(AddUserToTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> ChangeUserRoleAsync(ChangeUserRole request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> RemoveUserFromTenantAsync(RemoveUserFromTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> UpdateTenantAsync(UpdateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> SetTenantConfigurationAsync(SetTenantConfiguration request, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandStatusResult> GetStatusAsync(TenantCommandTrackingHandle handle, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandStatusResult.Unknown("Not used."));
    }

    /// <summary>
    /// Locates the shipped Tenants UI project directory by walking up from the test binary to the repository
    /// layout. Used by the component source scan, which reflection over declared dependencies cannot replace.
    /// </summary>
    private static string UiProjectRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "Hexalith.Tenants.slnx")))
        {
            directory = directory.Parent;
        }

        string root = Path.Combine(
            directory.ShouldNotBeNull("The repository root must be discoverable for the component source scan.").FullName,
            "src",
            "Hexalith.Tenants.UI");
        Directory.Exists(root).ShouldBeTrue($"The Tenants UI project source must be discoverable at {root}.");
        return root;
    }

    [Fact]
    public void Unconfigured_read_surface_resolves_as_disconnected_through_the_composed_container()
    {
        // Mutation-verified gap: flipping the no-base-address branch to IsConnected: true kept the whole
        // suite green. The two tests naming this behaviour construct TenantsBffComposition directly with a
        // hand-built availability record and never call AddHexalithTenantsUiModule; a third asserted only
        // the ServiceDescriptor. Only the connected branch was ever observed through a real container.
        // This flag gates the grant read-surface guard and both notification leases.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EventStore:BaseAddress"] = "https://eventstore.invalid",
            })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITenantsReadSurfaceAvailability>().IsConnected.ShouldBeFalse();
        scope.ServiceProvider.GetRequiredService<ITenantsBffComposition>().IsReadSurfaceConnected.ShouldBeFalse();
        scope.ServiceProvider.GetRequiredService<ITenantQueryGateway>()
            .ShouldBeOfType<UnavailableTenantQueryGateway>();
    }

    [Fact]
    public void Configured_read_surface_resolves_as_connected_through_the_composed_container()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tenants:BaseAddress"] = "https://tenants.invalid",
            })
            .Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITenantsReadSurfaceAvailability>().IsConnected.ShouldBeTrue();
        scope.ServiceProvider.GetRequiredService<ITenantsBffComposition>().IsReadSurfaceConnected.ShouldBeTrue();
    }

    [Fact]
    public void Host_override_declaring_a_connected_surface_over_the_unavailable_gateway_is_rejected()
    {
        // The presence-only XOR accepted this pairing, which is precisely the outcome requiring the pair is
        // documented to prevent: a host could claim a working read surface while registering the gateway
        // that cannot serve one.
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddScoped<ITenantQueryGateway, UnavailableTenantQueryGateway>();
        services.AddSingleton<ITenantsReadSurfaceAvailability>(new TenantsReadSurfaceAvailability(IsConnected: true));

        Should.Throw<InvalidOperationException>(
            () => services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false))
            .Message.ShouldContain("IsConnected: true");
    }

    [Fact]
    public void Host_override_declaring_a_disconnected_surface_over_a_connected_gateway_is_rejected()
    {
        // The converse direction, which no test covered at all.
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddScoped<ITenantQueryGateway, TenantQueryGateway>();
        services.AddSingleton<ITenantsReadSurfaceAvailability>(new TenantsReadSurfaceAvailability(IsConnected: false));

        Should.Throw<InvalidOperationException>(
            () => services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false))
            .Message.ShouldContain("IsConnected: false");
    }

    /// <summary>
    /// The agreement check must match the gateway registered as an instance, not only by type.
    /// </summary>
    /// <remarks>
    /// Both existing cases register the gateway by type, so matching only <c>ImplementationType</c> was
    /// sufficient for them -- and a host registering the unavailable gateway as an instance skipped the
    /// check entirely, which is the exact mismatched pairing the check exists to reject.
    /// </remarks>
    [Fact]
    public void Host_override_registering_the_unavailable_gateway_as_an_instance_is_still_checked()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddSingleton<ITenantQueryGateway>(new UnavailableTenantQueryGateway());
        services.AddSingleton<ITenantsReadSurfaceAvailability>(new TenantsReadSurfaceAvailability(IsConnected: true));

        Should.Throw<InvalidOperationException>(
            () => services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false))
            .Message.ShouldContain("IsConnected: true");
    }

    [Fact]
    public void Host_override_registering_the_gateway_through_a_factory_is_not_inverted_into_a_rejection()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddScoped<ITenantQueryGateway>(_ => new UnavailableTenantQueryGateway());
        services.AddSingleton<ITenantsReadSurfaceAvailability>(
            new TenantsReadSurfaceAvailability(IsConnected: false));

        Should.NotThrow(() =>
            services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false));

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantQueryGateway>()
            .ShouldBeOfType<UnavailableTenantQueryGateway>();
    }

    /// <summary>
    /// A factory-registered availability override is knowingly outside the agreement check.
    /// </summary>
    /// <remarks>
    /// Asking a factory for <c>IsConnected</c> means building a provider during composition, so its
    /// truthfulness is genuinely unknowable at this point. Recorded as an accepted limit rather than left to
    /// read as coverage: the pair requirement still applies, only the agreement check is skipped.
    /// </remarks>
    [Fact]
    public void KnownLimitation_host_override_registering_availability_through_a_factory_is_a_recorded_gap_not_a_rejection()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddScoped<ITenantQueryGateway, UnavailableTenantQueryGateway>();
        services.AddSingleton<ITenantsReadSurfaceAvailability>(
            _ => new TenantsReadSurfaceAvailability(IsConnected: true));

        // Deliberately does not throw. If this ever starts throwing, the limit above was closed and the
        // remark must be updated rather than the test relaxed.
        services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false);

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantQueryGateway>()
            .ShouldBeOfType<UnavailableTenantQueryGateway>();
    }

    [Fact]
    public void Host_override_with_an_agreeing_pair_is_accepted()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddScoped<ITenantQueryGateway, UnavailableTenantQueryGateway>();
        services.AddSingleton<ITenantsReadSurfaceAvailability>(new TenantsReadSurfaceAvailability(IsConnected: false));

        Should.NotThrow(() => services.AddHexalithTenantsUiModule(configuration, enableGatewayAuthorization: false));
    }
}
