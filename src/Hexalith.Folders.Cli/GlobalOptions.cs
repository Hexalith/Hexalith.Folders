namespace Hexalith.Folders.Cli;

/// <summary>
/// Parsed global option values shared across all subcommands. These are transport/observability concerns
/// only; tenant authority is carried by the bearer token's claims, never by these values.
/// </summary>
/// <param name="BaseAddress">The Folders REST base URL, or <see langword="null"/> when neither the flag nor env var supplied one.</param>
/// <param name="Token">The raw <c>--token</c> flag value (lowest credential precedence); may be <see langword="null"/>.</param>
/// <param name="CorrelationId">The explicit <c>--correlation-id</c> override, or <see langword="null"/> to generate a fresh ULID.</param>
/// <param name="Output">The selected output rendering mode.</param>
internal sealed record GlobalOptions(string? BaseAddress, string? Token, string? CorrelationId, OutputMode Output);
