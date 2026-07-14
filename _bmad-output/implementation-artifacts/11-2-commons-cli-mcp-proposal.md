# Commons.Cli and Commons.Mcp shared scaffolding proposal

Status: Proposed
Date: 2026-07-14
Owner: Hexalith.Commons platform maintainers
Origin: Folders Story 11.2, gaps G4 and G5

Tracking: [Hexalith.Commons issue 25](https://github.com/Hexalith/Hexalith.Commons/issues/25) and [Hexalith.Commons issue 26](https://github.com/Hexalith/Hexalith.Commons/issues/26)

## Decision summary

Create two small, composable Commons packages after this proposal is accepted:

- `Hexalith.Commons.Cli` owns command-root construction, recursive global options, per-key configuration resolution, output formatting, safe process I/O, and exit-code policy contracts.
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
| `--token-file` | Read token from an explicitly selected file | Absent |
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

Use the following first-nonblank order for CLI and MCP; sources that a surface cannot have are simply absent:

1. explicit `--token`;
2. explicit `--token-file`;
3. the adapter-specific token environment variable, then `HEXALITH_TOKEN` as the shared fallback;
4. token reference in the selected secure credential profile/store;
5. configured token-file reference;
6. workload identity or another adapter-registered non-interactive provider;
7. missing credential.

This chooses explicit invocation intent over ambient state and aligns with Memories. It deliberately changes the eventual Folders migration from its current environment → credential file → flag order; that change must occur only in an explicit adoption story with parity fixtures and release notes.

Inline plaintext tokens in ordinary JSON/appsettings configuration are disabled by default. A compatibility adapter may opt in temporarily, below environment and secure-file sources, with a deprecation diagnostic that never includes the token. `--token` remains for compatibility but documentation should prefer environment, protected token files, stdin/secret-agent integrations, or workload identity because command-line arguments may be visible to other local processes.

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
- `SysexitsExitCodePolicy`: configurable mappings in the Folders-style `0`, `1`, and `64–78` range.

Applications pin a policy explicitly. A future platform-wide numeric unification requires its own ADR and migration plan.

## `Hexalith.Commons.Mcp`

Expose an `AddHexalithMcpAdapter`/builder composition seam that:

1. configures MCP transport without owning domain tool registration;
2. keeps stdout exclusively for JSON-RPC and routes logs to stderr;
3. shares the configuration and credential-source pipeline from Commons.Cli without taking a dependency on `System.CommandLine` at runtime;
4. registers safe bearer/Dapr invocation handlers and propagates caller cancellation;
5. maps authentication, authorization, validation, rate-limit, unavailable, and unexpected failures to stable metadata-only MCP errors;
6. rejects raw exception messages, token values, request/response bodies, provider paths, and tenant-sensitive identifiers from diagnostics;
7. supports hermetic environment, file, clock, and transport substitutes.

The configuration/credential primitives should live in a small shared assembly or in Commons.Mcp abstractions so the MCP package does not pull CLI parsing into a server process.

MCP does not use process exit codes for individual tool calls. It uses the same semantic outcome taxonomy for protocol error projection and reserves process exit status for bootstrap failure.

## Compatibility and rollout

1. Implement the contracts and test kits in Commons without changing a product adapter.
2. Pilot with one non-Folders adapter and publish packages plus central `Hexalith.Builds` versions.
3. Complete Folders Story 11.6 as an in-repository consolidation against existing wire/parity contracts.
4. Open a separate adoption story that compares Folders CLI/MCP behavior with the Commons contracts, explicitly approves the credential-precedence change, and updates parity fixtures.
5. Migrate other adapters one at a time; preserve aliases and output/exit compatibility through explicit policies.

## Required verification for implementation

- option alias/recursion and command-composition tests;
- independent per-key source precedence and invalid-higher-source fail-closed tests;
- exhaustive credential precedence, blank values, unreadable files, provider outcomes, and no-secret-output tests;
- culture-invariant JSON/CSV/table golden vectors and redirected-output behavior;
- exit-policy compatibility vectors;
- MCP stdout purity, stderr logging, cancellation, safe error projection, and hermetic transport tests;
- package-consumption tests through the pinned `Hexalith.Builds` versions.

## Explicit non-goals

- No implementation is delivered by Folders Story 11.2.
- No Folders CLI/MCP source changes are authorized here.
- No domain command, DTO, generated SDK, authorization rule, or product error category moves into Commons.
- No existing adapter changes credential precedence or numeric exit codes merely by referencing a package.
