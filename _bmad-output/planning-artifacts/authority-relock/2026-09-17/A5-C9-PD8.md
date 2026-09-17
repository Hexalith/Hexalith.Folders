# A5 / C9 / PD8 Decision Payload

Version: `1.0.0-candidate.1`

Before any persistence, the EventStore event-write boundary replaces each confidential operational value with a fixed-width, tenant-scoped HMAC-SHA-256 correlation token over a versioned classification tag, canonical field identity, and NFC-normalized value. No event, actor state, projection, audit record, log, trace, export, backup, secret store, working-copy persistence, retry payload, or other durable facility may contain the cleartext or a reversibly encrypted confidential value.

The token minted for a binding/value generation is its immutable canonical correlation and writer token. During key rotation, an authorized presented value is compared under active and previous keys; a previous-key match transactionally adds an active-key lookup alias to the unchanged canonical token. An old token key may retire only after every live binding has active alias coverage or is expired/deleted and all old lease/retry windows are closed. Incomplete coverage, ambiguity, or collision fails closed.

Durable provider execution may retain only the canonical token, ordinary credential references, and provider-issued opaque resource or operation handles that neither encode nor permit recovery of the confidential value. Each provider operation and confidential field is classified `opaque-handle-restartable` or `cleartext-required`. A `cleartext-required` operation is rejected before EventStore admission or external side effect with `confidential_operation_not_durable` (HTTP 422, category `validation_error`, retryable false, client action `correct_input`). Transient plaintext may exist only in bounded process memory for preflight and token comparison and must never enter a working copy or durable retry state.

Surfaces render `withheld` plus the canonical token, distinct from `redacted`, `unavailable`, and `absent`. Any future reversible recovery design is a material Product, Architecture, Security, and Legal decision and is not authorized by this payload.

