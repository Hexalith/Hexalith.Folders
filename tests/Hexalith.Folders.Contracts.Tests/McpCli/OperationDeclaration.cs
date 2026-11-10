namespace Hexalith.Folders.Contracts.Tests.McpCli;

/// <summary>Independent owner-approved routing and spine mapping for an inventory candidate.</summary>
/// <param name="Id">The immutable inventory row.</param>
/// <param name="Type">The declared CLR type.</param>
/// <param name="Part">The canonical operation part.</param>
/// <param name="SpineOperation">The mapped spine operation or component.</param>
/// <param name="AggregateIdProperty">The declared aggregate source, when present.</param>
/// <param name="TaskScoped">Whether the command catalog requires a task.</param>
internal sealed record OperationDeclaration(string Id, Type Type, string Part, string SpineOperation,
    string? AggregateIdProperty, bool TaskScoped);
