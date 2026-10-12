# Story 12.1: deployed organization baseline authority

**Status:** approved by the owner on 2026-10-12. The selected rule is current TenantOwner membership plus explicit `manage_organization_acl` permission in the verified JWT, with an event-derived durable permission view. The signed human receipt was approved separately and is implemented in source.

## Evidence

In a fresh isolated Aspire run, EventStore accepted Tenants bootstrap, tenant creation, and tenant membership commands. Folders Workers received the `tenants.events` messages and wrote a shared, persisted tenant-access projection. EventStore's signed human receipt passed Folders `/process`. `CreateFolder` then received Folders HTTP 404 at the folder permission layer because the deployed `IEffectivePermissionsReadModel` is in-memory and contains no `organization_baseline` grant. The EventStore gateway surfaced the downstream safe denial as HTTP 500. No folder event or end state was persisted.

The existing Story 2.2 `OrganizationAggregate` defines `InitializeOrganizationAclBaseline`, `GrantOrganizationAclPrincipal`, `RevokeOrganizationAclPrincipal`, and metadata-only organization events. These commands are not routed through the deployed EventStore/Folders `/process` path, and no deployed projector converts those events into the current effective-permissions evidence. Story 2.3 requires an organization ACL `create_folder` grant; tenant membership or a JWT permission alone is insufficient.

## Recommended continuation contract

1. Add EventStore-routed organization ACL initialization and grant/revoke commands. The first initialization uses the verified receipt, current tenant-access projection, and an explicit tenant administrator rule before organization stream lookup. It does not infer a `create_folder` grant from tenant membership, a JWT claim, or a demo seed. The selected rule must be named and tested; the natural candidate is current `TenantOwner` membership plus an explicit ACL-management permission in the verified JWT.
2. Persist accepted metadata-only organization ACL events through the EventStore actor. Derive the `organization_baseline` effective-permissions view from those events through the EventStore SDK projection/read-model store. Folders Server reads that durable view for each mutation and fails closed on stale, malformed, missing, or unavailable evidence. Folder-specific effective permissions continue to require their own authoritative projection.
3. Submit the initialization command through the same authenticated gateway to establish one explicit `create_folder` grant for the live test caller. Re-run the command, inspect the EventStore stream and state, then run the specified restart, replay, replica, timeout, idempotency, and leakage probes.
4. Define a trusted, bounded downstream denial transport so Folders' metadata-only 401/403/404/503 result is surfaced by EventStore as the corresponding canonical gateway result without marking an authorization denial as an infrastructure dead letter. The gateway must not pass through arbitrary domain-service response bytes.

## Owner choice

Approve the tenant-administrator bootstrap rule and event-derived effective-permissions approach above, or specify the existing authoritative ACL provisioning path to use. Either path must produce a real organization event and current durable permission evidence before Story 12.1 can be accepted. The current implementation remains in progress until that path and the deployed acceptance checks pass.
