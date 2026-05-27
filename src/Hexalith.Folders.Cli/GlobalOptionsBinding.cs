using System;
using System.CommandLine;

namespace Hexalith.Folders.Cli;

/// <summary>
/// Creates the recursive global <see cref="Option{T}"/> instances and resolves them from a parse result.
/// Mirrors the <c>Hexalith.EventStore.Admin.Cli</c> binding shape (recursive options, env-var
/// <c>DefaultValueFactory</c>, <c>AcceptOnlyFromAmong</c>) but introduces none of its profile store or
/// exit-code scheme.
/// </summary>
internal sealed class GlobalOptionsBinding
{
    private const string BaseAddressEnvVar = "HEXALITH_FOLDERS_BASE_ADDRESS";

    private GlobalOptionsBinding(
        Option<string?> baseAddressOption,
        Option<string?> tokenOption,
        Option<string?> correlationIdOption,
        Option<string> outputOption)
    {
        BaseAddressOption = baseAddressOption;
        TokenOption = tokenOption;
        CorrelationIdOption = correlationIdOption;
        OutputOption = outputOption;
    }

    /// <summary>Gets the Folders REST base-URL option (env fallback <c>HEXALITH_FOLDERS_BASE_ADDRESS</c>).</summary>
    public Option<string?> BaseAddressOption { get; }

    /// <summary>Gets the bearer-token option (lowest credential precedence; env/file checked first by the resolver).</summary>
    public Option<string?> TokenOption { get; }

    /// <summary>Gets the explicit correlation-ID override option.</summary>
    public Option<string?> CorrelationIdOption { get; }

    /// <summary>Gets the output-mode option (<c>human</c> default, <c>json</c>).</summary>
    public Option<string> OutputOption { get; }

    /// <summary>Creates the global option definitions with environment-variable fallbacks and constraints.</summary>
    /// <returns>A new binding.</returns>
    public static GlobalOptionsBinding Create()
    {
        Option<string?> baseAddressOption = new("--base-address", "-b")
        {
            Description = "Folders REST base URL (absolute). Falls back to the " + BaseAddressEnvVar + " environment variable.",
            Recursive = true,
            DefaultValueFactory = _ => Environment.GetEnvironmentVariable(BaseAddressEnvVar),
        };

        Option<string?> tokenOption = new("--token", "-t")
        {
            Description = "Bearer JWT. Lowest precedence: HEXALITH_TOKEN env var, then ~/.hexalith/credentials.json, then this flag.",
            Recursive = true,
        };

        Option<string?> correlationIdOption = new("--correlation-id")
        {
            Description = "Correlation ID propagated to all SDK sub-calls. When omitted a fresh ULID is generated per invocation.",
            Recursive = true,
        };

        Option<string> outputOption = new("--output", "-o")
        {
            Description = "Output rendering mode.",
            Recursive = true,
            DefaultValueFactory = _ => "human",
        };
        _ = outputOption.AcceptOnlyFromAmong("human", "json");

        return new GlobalOptionsBinding(baseAddressOption, tokenOption, correlationIdOption, outputOption);
    }

    /// <summary>Registers the global options on the supplied root command.</summary>
    /// <param name="rootCommand">The root command.</param>
    public void AddToRoot(RootCommand rootCommand)
    {
        ArgumentNullException.ThrowIfNull(rootCommand);
        rootCommand.Options.Add(BaseAddressOption);
        rootCommand.Options.Add(TokenOption);
        rootCommand.Options.Add(CorrelationIdOption);
        rootCommand.Options.Add(OutputOption);
    }

    /// <summary>Resolves the parsed global option values.</summary>
    /// <param name="parseResult">The parse result.</param>
    /// <returns>The resolved <see cref="GlobalOptions"/>.</returns>
    public GlobalOptions Resolve(ParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        OutputMode output = string.Equals(parseResult.GetValue(OutputOption), "json", StringComparison.Ordinal)
            ? OutputMode.Json
            : OutputMode.Human;
        return new GlobalOptions(
            parseResult.GetValue(BaseAddressOption),
            parseResult.GetValue(TokenOption),
            parseResult.GetValue(CorrelationIdOption),
            output);
    }
}
