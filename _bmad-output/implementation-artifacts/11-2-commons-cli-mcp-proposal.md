# Commons.Cli and Commons.Mcp shared scaffolding proposal

Status: Proposed
Date: 2026-07-14
Owner: Hexalith.Commons platform maintainers
Origin: Folders Story 11.2, gaps G4 and G5

Tracking: [Hexalith.Commons issue 25](https://github.com/Hexalith/Hexalith.Commons/issues/25) and [Hexalith.Commons issue 26](https://github.com/Hexalith/Hexalith.Commons/issues/26)

## Decision summary

Create two small, composable Commons packages after this proposal is accepted:

- `Hexalith.Commons.Cli` owns command-root construction, recursive global options, per-key configuration resolution, output formatting, safe process I/O (capped token-file reads, argv/env redaction), and exit-code policy contracts.
- `Hexalith.Commons.Mcp` owns MCP host/bootstrap composition, stdio isolation, safe protocol-error projection, shared credential resolution, and transport registration.

The packages provide adapter mechanics, not domain commands, generated clients, tenant policy, error taxonomies, or product-specific configuration. Folders Story 11.6 continues to build its in-repository CLI/MCP adapter core. Migration from that core to Commons is explicitly deferred until the Commons packages are implemented, released, and adopted by a separate story.

## Context

The current adapters repeat the same composition with incompatible details:

- Folders CLI mirrors EventStore Admin CLI recursive `System.CommandLine` bindings but has its own output and sysexits-style domain map.
- EventStore Admin CLI already has table/JSON/CSV formatters and a simple `0/1/2` exit convention.
- Memories CLI has a reusable per-key source pipeline and uses flag → environment → file → default precedence.
- Folders CLI uses environment → credential store → explicit `--token`, making the explicit invocation override the lowest-priority source.
- Folders and Memories MCP hosts repeat authentication, ProblemDetails, Dapr invocation, logging, and stdout-safety setup.

One package that hard-codes a domain command tree or one numeric exit table would merely move the coupling. The shared layer therefore standardizes the mechanics and semantic categories while leaving domain projections injectable.

## `Hexalith.Commons.Cli`

### Root-command builder

Expose a builder such as `HexalithCliApplicationBuilder` with these responsibilities:

1. Create a `System.CommandLine.RootCommand` with stable application name, description, version, cancellation, and exception boundaries.
2. Register one instance of every recursive global option so nested commands do not create conflicting aliases.
3. Accept domain command factories through callbacks or an `ICliCommandModule` contract.
4. Resolve global configuration once per invocation and expose an immutable `CliInvocationContext` to handlers.
5. Keep stdout for result data, stderr for diagnostics, and never write secrets to either channel.
6. Map handler cancellation to a non-success semantic outcome (not `Success`). Map unhandled exceptions to `Unexpected` after redaction. Never emit `Success` from a cancelled or faulted handler.

The builder must not discover commands by reflection. Registration order and aliases remain explicit and testable.

### Global options

The standard option set is:

| Option | Purpose | Default |
| --- | --- | --- |
| `--endpoint` / adapter alias such as `--base-address` | Absolute service endpoint | Adapter-provided non-secret default or configuration |
| `--tenant` | Tenant/profile selector | Configuration or adapter default |
| `--correlation-id` | Explicit correlation override | Fresh adapter-approved identifier per invocation |
| `--output`, `-o` | `human`, `table`, `json`, or `csv` | `human` when interactive; adapter may pin another mode |
| `--profile` | Named configuration profile | `default` |
| `--token` | Compatibility bearer-token override | Absent |
| `--token-file` | Read token from an explicitly selected **regular file** | Absent |

`--token-file` MUST: refuse non-regular files (devices, FIFOs, directories); cap the read (recommended 64 KiB); fail closed on unreadable/oversize files; never log the path’s contents. `--token` and `--token-file` MUST be redacted from process-list diagnostics, verbose output, and exception messages because argv is visible to other local processes. Do not spawn child processes to obtain tokens unless the adapter registers a named secret-agent with stdout capture and secret redaction.
| `--verbose` | Metadata-only diagnostic detail | `false` |

Adapters may hide unsupported options, add aliases, or add domain options. They must not change the meaning of a standard option.

### Configuration layering

Resolve every key independently; an endpoint from one source must not force the token to come from that source. The generic order, highest priority first, is:

1. explicit command option;
2. environment variable;
3. selected profile or configuration file;
4. application default, for non-secret settings only.

Expose the winning source as a non-sensitive enum (`CommandOption`, `Environment`, `Profile`, `Default`, `Missing`) for diagnostics. Never expose source paths, section names that contain tenant data, or values.

Invalid values at a higher-priority explicit source fail closed; they do not silently fall through. Missing values fall through. File parsing, endpoint validation, and option binding remain injectable for hermetic tests.

### Harmonized credential precedence

Use the following first-nonblank order for CLI and MCP; sources that a surface cannot have are simply absent. **Missing** falls through. **Denied** or **Unavailable** from a credential store or secret-agent **stops the chain** and returns that outcome — it must not fall through to a lower token.

1. explicit `--token`;
2. explicit `--token-file`;
3. stdin or a registered secret-agent, when the surface has that source;
4. the adapter-specific token environment variable, then `HEXALITH_TOKEN` as the shared fallback;
5. token reference in the selected secure credential profile/store;
6. configured token-file reference;
7. compatibility inline plaintext JSON/appsettings token, **opt-in only**, with a deprecation diagnostic that never includes the token (disabled by default; sits below environment and secure-file, above workload identity);
8. workload identity or another adapter-registered non-interactive provider;
9. missing credential.

This chooses explicit invocation intent over ambient state and aligns with Memories. It deliberately changes the eventual Folders migration from its current environment → credential file → flag order; that change must occur only in an explicit adoption story with parity fixtures and release notes.

`--token` remains for compatibility but documentation should prefer environment, protected token files, stdin/secret-agent integrations, or workload identity.

The resolver returns a disposable/opaque `ResolvedCredential` plus a source enum. It never implements `ToString()` with the secret, never serializes the value, and clears owned buffers where practical. Missing/denied/unavailable outcomes remain distinct.

### Output formatting

Expose `IOutputFormatter`, `IOutputWriter`, and a typed column model:

- `human`: adapter-authored concise prose;
- `table`: stable columns, terminal-width-aware rendering, no ANSI when redirected;
- `json`: UTF-8, invariant, machine-stable property names, one complete JSON value;
- `csv`: RFC 4180 quoting, invariant values, stable header order.

Formatters receive projections, never exceptions or raw HTTP bodies. Errors go to stderr as a safe semantic projection; successful data goes to stdout. JSON/CSV modes must not mix prose with machine output. Redaction happens before formatting, and formatters reject values marked sensitive.

### Exit-code policy

Commons defines semantic outcomes, not one mandatory numeric table:

- `Success`
- `Degraded`
- `UsageOrConfiguration`
- `CredentialFailure`
- `AccessDenied`
- `Conflict`
- `ValidationFailure`
- `UpstreamFailure`
- `UnavailableOrReconciliationRequired`
- `NotFound`
- `Redacted`
- `Unexpected`

`ICliExitCodePolicy` maps these outcomes to integers. Ship two named compatibility policies rather than silently changing applications:

- `SimpleExitCodePolicy`: EventStore-compatible `Success=0`, `Degraded=1`, all failures `=2`.
- `SysexitsExitCodePolicy`: Folders-compatible default map in the `0`, `1`, and `64–76` range. Default integers (overridable only by an explicit policy subclass, never by silent omission):
  - `Success=0`
  - `Degraded=1` (or adapter-pinned)
  - `UsageOrConfiguration=64`
  - `CredentialFailure=65`
  - `AccessDenied=66`
  - `Conflict=68`
  - `ValidationFailure=69`
  - `UpstreamFailure=70`
  - `UnavailableOrReconciliationRequired=72` (HTTP 429 / rate-limit maps here — retryable; **do not** add a separate `RateLimited` CLI outcome)
  - `NotFound=73`
  - `Redacted=75`
  - `Unexpected=1`
  Unmapped semantic outcomes MUST fail closed (throw in debug, `Unexpected` in production) rather than emit an undefined integer.

Applications pin a policy explicitly. A future platform-wide numeric unification requires its own ADR and migration plan.

## `Hexalith.Commons.Mcp`

Expose an `AddHexalithMcpAdapter`/builder composition seam that:

1. configures MCP transport without owning domain tool registration;
2. keeps stdout exclusively for JSON-RPC and routes logs to stderr;
3. shares the configuration and credential-source pipeline from Commons.Cli without taking a dependency on `System.CommandLine` at runtime;
4. registers safe bearer/Dapr invocation handlers and propagates caller cancellation;
5. maps authentication, authorization, validation, conflict, not-found, degraded, rate-limit (as unavailable/retryable), unavailable, and unexpected failures to stable metadata-only MCP errors;
6. rejects raw exception messages, token values, request/response bodies, provider paths, and tenant-sensitive identifiers from diagnostics;
7. supports hermetic environment, file, clock, and transport substitutes.

The configuration/credential primitives should live in a small shared assembly or in Commons.Mcp abstractions so the MCP package does not pull CLI parsing into a server process.

MCP does not use process exit codes for individual tool calls. It uses the same semantic outcome taxonomy for protocol error projection. Bootstrap/host process failure MUST pin process exit status through `ICliExitCodePolicy` (or the same named numeric map): configuration/credential failure uses that policy’s `UsageOrConfiguration`/`CredentialFailure`; unexpected bootstrap faults use `Unexpected`. Do not leave bootstrap exits adapter-defined.

## Compatibility and rollout

1. Implement the contracts and test kits in Commons without changing a product adapter.
2. Pilot with one non-Folders adapter and publish packages plus central `Hexalith.Builds` versions.
3. Complete Folders Story 11.6 as an in-repository consolidation against existing wire/parity contracts.
4. Open a separate adoption story that compares Folders CLI/MCP behavior with the Commons contracts, explicitly approves the credential-precedence change, and updates parity fixtures.
5. Migrate other adapters one at a time; preserve aliases and output/exit compatibility through explicit policies.

## Required verification for implementation

- option alias/recursion and command-composition tests;
- independent per-key source precedence and invalid-higher-source fail-closed tests;
- exhaustive credential precedence, blank values, Denied/Unavailable stop-the-chain, unreadable/oversize token files, argv redaction, provider outcomes, and no-secret-output tests;
- culture-invariant JSON/CSV/table golden vectors and redirected-output behavior;
- exit-policy compatibility vectors, including HTTP 429 → `UnavailableOrReconciliationRequired` and unmapped-outcome fail-closed;
- MCP stdout purity, stderr logging, cancellation, Conflict/NotFound/Degraded/rate-limit error projection, bootstrap `ICliExitCodePolicy` exits, and hermetic transport tests;
- package-consumption tests through the pinned `Hexalith.Builds` versions.

## Explicit non-goals

- No implementation is delivered by Folders Story 11.2.
- No Folders CLI/MCP source changes are authorized here.
- No domain command, DTO, generated SDK, authorization rule, or product error category moves into Commons.
- No existing adapter changes credential precedence or numeric exit codes merely by referencing a package.
