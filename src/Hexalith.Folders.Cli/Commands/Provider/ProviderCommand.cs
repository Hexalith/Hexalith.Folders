using System.CommandLine;

using Hexalith.Folders.Client.Generated;

namespace Hexalith.Folders.Cli.Commands.Provider;

/// <summary>
/// The <c>provider</c> command group: provider-binding configuration and provider readiness/support
/// queries. Each subcommand wraps the matching <see cref="IClient"/> operation per the Command-to-SDK map.
/// </summary>
internal static class ProviderCommand
{
    /// <summary>Creates the <c>provider</c> group command.</summary>
    /// <param name="pipeline">The shared command pipeline.</param>
    /// <param name="global">The global options binding.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(CommandPipeline pipeline, GlobalOptionsBinding global)
    {
        Command command = new("provider", "Configure provider bindings and inspect provider readiness.");

        Option<string> configureRef = CommandOptions.RequiredId("--provider-binding-ref", "Opaque provider binding reference.");
        Option<string?> configureBody = CommandOptions.Request();
        command.Subcommands.Add(CommandFactory.Mutation(
            "configure-binding",
            "Configure a provider binding (mutating).",
            pipeline,
            global,
            [configureRef, configureBody],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.ConfigureProviderBindingAsync(
                parseResult.GetValue(configureRef)!,
                sourcing.IdempotencyKey,
                sourcing.CorrelationId,
                sourcing.TaskId,
                CommandOptions.ReadBody<ConfigureProviderBindingRequest>(parseResult.GetValue(configureBody)),
                ct))));

        Option<string> getRef = CommandOptions.RequiredId("--provider-binding-ref", "Opaque provider binding reference.");
        Option<string?> getFreshness = CommandOptions.Freshness();
        command.Subcommands.Add(CommandFactory.Query(
            "get-binding",
            "Get a provider binding (query).",
            pipeline,
            global,
            taskIdRequired: false,
            [getRef, getFreshness],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.GetProviderBindingAsync(
                parseResult.GetValue(getRef)!,
                sourcing.CorrelationId,
                CommandOptions.ParseFreshness(parseResult.GetValue(getFreshness)),
                ct))));

        Option<string?> readinessFreshness = CommandOptions.Freshness();
        Option<string?> readinessBody = CommandOptions.Request();
        command.Subcommands.Add(CommandFactory.Query(
            "validate-readiness",
            "Validate provider readiness (query; no idempotency key).",
            pipeline,
            global,
            taskIdRequired: false,
            [readinessFreshness, readinessBody],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.ValidateProviderReadinessAsync(
                sourcing.CorrelationId,
                CommandOptions.ParseFreshness(parseResult.GetValue(readinessFreshness)),
                CommandOptions.ReadBody<ValidateProviderReadinessRequest>(parseResult.GetValue(readinessBody)),
                ct))));

        Option<string?> evidenceFreshness = CommandOptions.Freshness();
        Option<string?> evidenceCursor = CommandOptions.Cursor();
        Option<int?> evidenceLimit = CommandOptions.Limit();
        command.Subcommands.Add(CommandFactory.Query(
            "support-evidence",
            "List provider support evidence (query).",
            pipeline,
            global,
            taskIdRequired: false,
            [evidenceFreshness, evidenceCursor, evidenceLimit],
            (parseResult, client, sourcing, ct) => CommandFactory.AsObject(client.GetProviderSupportEvidenceAsync(
                sourcing.CorrelationId,
                CommandOptions.ParseFreshness(parseResult.GetValue(evidenceFreshness)),
                parseResult.GetValue(evidenceCursor)!,
                parseResult.GetValue(evidenceLimit),
                ct))));

        return command;
    }
}
