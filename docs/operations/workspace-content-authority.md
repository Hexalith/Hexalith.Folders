# Workspace content authority

The EventStore DomainService content SDK owns physical bodies and its separate metadata ledger. Folders
events retain verified receipts, content hashes, lengths, versions and policy digests. Bodies never belong
in events, snapshots, projections, audit output or disposable working copies. Debug qualification uses the
root-declared EventStore source dependency; no package version or submodule pin is changed by this story.

Production requires an explicit `Folders:WorkspaceFilePolicy` configuration. Set `Version` to `1.1.0`,
`ProtectionMode` to `non_confidential`, and a finite `Includes` allowlist. Optional `Excludes`, `Restricted`
and `MetadataOnly` arrays use the approved workspace-root glob grammar. The combined rule count is 1–100,
each rule is at most 256 characters and the combined character limit is 25,600. There is no permissive
default. Missing, invalid or changing policy fails closed. `confidential_cleartext_required` rejects durable
execution before upload; the Story 12.7 token capability is required before such execution can be enabled.

The existing v1 add/change routes accept JSON inline bytes through 262,144 decoded bytes. Larger bodies,
through 1,048,576 bytes, use multipart form data with exactly two ordered sections: `metadata` (JSON, at most
16 KiB) then `content` (actual bytes). The declared hash and length must match the actual bytes. A JSON stream
descriptor can only refer to an existing SDK-verified receipt; a caller assertion is insufficient. The
client `UploadWorkspaceFileAsync` helper chooses this transport from an actual stream. The caller supplies
the observed `expectedLockFence` and `expectedFileVersion`; the latter is zero for an add. Context metadata
returns the immutable `fileId` and `version`, and lock status returns `lockFence`. Public v2 stays disabled.

The internal `MutateFiles` actor command also accepts `{requestSchemaVersion, workspaceId, taskId, items}` with
1–100 caller-ordered metadata/receipt items and at most 10,485,760 add/change bytes. Operation IDs and paths
must be unique under ordinal-ignore-case comparison. All path, authorization, receipt and version checks
finish before one SDK preparation and one transactional EventStore event append. It has no public batch route.
Unresolved preparations remain unreadable and protected until the SDK reconciles the authoritative decision.

Content administration uses authenticated `POST
/api/v1/folders/{folderId}/workspaces/{workspaceId}/content/disposition` with JSON
`{"receiptId":"content_<64 lower hex>","operation":"hold|release|abandon|apply"}`. This separate administrative
transport requires current `manage_folder_access` authority on every SDK operation; possession of a receipt
grants no authority. Success is HTTP 200 `{"accepted":true,"erased":false}` (or `erased:true` after physical
deletion). Safe authorization denial is 404; conflicts are 409; unavailable authoritative evidence is 503.
No path, byte or credential is included in errors. This route is independent of the one-item file operation
contract and enables no public v2 operation.
Its exact transport schema is [workspace-content-administration-v1.yaml](../contract/workspace-content-administration-v1.yaml).

`hold` and `release` use the SDK's content-specific hold ledger. `abandon` only seals an upload that never
became associated work; assigned tasks instead require authoritative terminal proof. `apply` reconciles
publication and evaluates the approved P7D disposition. It never erases live references, active or unresolved
work, held bytes or bytes without a complete fresh epoch. Release starts a fresh epoch. Immediate logical
suppression is independent of physical erasure. At erasure the SDK reserves the receipt against new holds
and references, then Folders serializes a source-generation grant in the owning actor before deletion.
See [retention and tenant deletion](retention-and-tenant-deletion.md) for the authoritative-body class.

Tree, metadata and glob queries read current source-bound metadata; search and range reads require current
`read_file_content` authority. Paths are classified before response ordering and counts. Binary, invalid
UTF-8 and prohibited controls preserve their exact mutation bytes but are metadata only. All context reads
are bounded and fail closed for missing/corrupt bodies, source drift, policy drift, timeouts or unavailable
authority. A truncated result has no continuation token; callers refine their query.
