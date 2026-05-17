namespace Hexalith.Folders.Client.Generated;

// NSwag duplicate-name compatibility shim for ChangedPathEvidence references in AuditRecord
// and WorkspaceDiagnostic. The Contract Spine references the same ChangedPathEvidence schema
// from multiple paths; NSwag's C# emitter currently emits a duplicate `ChangedPathEvidence2`
// type alongside the canonical `ChangedPathEvidence` rather than reusing the reference.
// This shim declares ChangedPathEvidence2 as a derived alias so the generated client compiles
// without rejecting the duplicate emission. Remove when the Contract Spine or NSwag
// configuration produces one stable ChangedPathEvidence type.
// Round 4 review finding P14: this hand-written shim lives outside Generated/ so the generated
// file stays purely generated.
// Namespace: must remain Hexalith.Folders.Client.Generated because the generated client
// (`HexalithFoldersClient.g.cs:11396, 11671`) references ChangedPathEvidence2 unqualified.
// Moving the namespace to .Compat would require injecting a `using` directive into the
// generated file, contradicting the no-edit-generated rule.
public partial class ChangedPathEvidence2 : ChangedPathEvidence
{
}
