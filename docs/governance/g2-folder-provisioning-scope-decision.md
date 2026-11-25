# G-2 Folder provisioning scope decision

On 2026-11-25 Jerome, acting as Folders Owner, approved the G-2 Folder provisioning design, implementation, and isolated qualification scope requested by the Projects 7.1-P2 owner proposals.

The scope covers create-only provisioning with an expected owner version or create precondition, current original-actor read and manage authority, a deterministic task-step key, and a durable receipt with authoritative status lookup. Provisioning creates with the original actor as sole manager and no Tenant-default read. Record an explicit compensation disposition and retain the Folder and receipt when activation remains unresolved.

The delivery checks require persisted tests for equal retry, changed request, stale version, name collision, cross-tenant and revoked authority, dependency failure, and lost response recovered after restart. Exercise rollback or forward recovery against the retained store and verify the incumbent Folder and committed history. Capture the source, contract and dependency versions, commands, native results and owner acceptance.

The inspected Folders source was `d56e868fd38c1c84f447180b8bfbe525235220f7`. The proposal is `Hexalith.Projects/_bmad-output/implementation-artifacts/7-1-p2-owner-scope-proposals.md` at `fdc8ca69ebe0a656f88d55f9e32c8de71b1262f6`. This decision authorizes work in the isolated qualification scope. Capability acceptance is pending.

## Provisioning design investigation

Use a separate create-only operation through the EventStore Folder actor. A new request targets an absent owner identity, binds the deterministic task-step key and intent to that identity, and records the restricted access mode and provision receipt with the create event. An authorized equivalent retry resolves the receipt from committed history before checking the first-create precondition. An incumbent stream or changed intent yields a safe conflict. A display-name lookup cannot select an existing Folder. The current generic creation path derives a Folder identity from the Tenant and idempotency key and does not reserve display names. An owner-wide name uniqueness rule would change the existing creation contract and requires its own decision.

`FolderCreationService` already checks the Organization grant and reauthorizes before append. `EventStoreFolderEffectivePermissionsReadModel` combines Organization baseline grants with Folder overrides. The provisioning mode needs to exclude inherited grants and seed explicit read and manage grants for the original actor in the authoritative create. The receipt lookup also needs fresh, exact read and manage evidence before returning the Folder identity. `FolderOwnerReadQueryHandler` currently provides the delegated metadata-read seam.

The current EventStore trusted effect origin authenticates the workload, purpose, source and target, and command bytes. It carries no verified original actor claim. Before connecting delegated provisioning, select a task-bound credential or committed admission lookup that independently binds the original actor to the Tenant, workload, task step, operation and target. Folders must re-evaluate current actor authority at the operation. Compensation disposition for the proposed create is to retain the Folder and receipt in unresolved workflows. Before recovery, fence any runtime whose projection could reapply inherited read grants to a restricted Folder.

## Decisions (2026-11-25)

On 2026-11-25 Jerome, acting as Folders Owner, settled the open provisioning questions.

- **Name collisions.** Display names are not unique. A collision is an existing Folder stream at the identity derived for the task step, or a changed request under the same step key. Both return the safe conflict and never select or modify the incumbent Folder. Two provisioned Folders can share a display name, so consumers tell Folders apart by identity. The generic creation contract is unchanged.
- **Rollback target.** Rollback after restricted provisioning is forward-only. Disable new provisioning at the same revision, freeze Folder mutations and fix forward. A previous runtime that cannot interpret the restricted access mode must not serve Folders created with it, because its permission projection could reapply inherited Tenant read grants.
- **Original actor.** Delegated provisioning verifies the original actor through the committed EventStore admission record for the task step, not a task-bound token. Folders then re-evaluates the actor's current read and manage authority at the operation.
