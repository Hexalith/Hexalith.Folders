# Domain enrollment SM-2 record — pending

This template records human evidence for Story 3.11. Static adoption validation is
bounded and does not establish runtime behavior. Replace pending values only with
observed evidence; attach source audit notes and diagnostic paths.

| Evidence | Owner-recorded value |
| --- | --- |
| Module / repository / revision / dirty state | `folders` / `Hexalith.Folders` / `aef674d3` (pending clean committed enrollment revision) |
| Direct Platform revision / release tag | `3848af49f9afc91b174b4c716751fe49cb15dbba` (`v2.0.0-eventstore.1-382-g3848af49`) — pending revision with `folders` bindings |
| Accountable module owner / Platform owner / platform-handoff.md reference | `@jpiquot` / `@jpiquot` / `module/platform-handoff.md` (`pending`) |
| Reviewed direct Platform revision containing executable/image registration | pending |
| Server-scoped configuration / authentication/signing propagation / token adapters | pending (inventoried in `module/platform-handoff.md`; Platform adapter implementation pending) |
| Gateway tenant/domain/version/appId/method tuple / preservation and filtering review | pending (`domain: folders`, `appId: folders`, methods `/process`, `/query`, `/project`, `/project/v2`, `/admin/operational-index-metadata`) |
| Dapr static/model policy evidence / separate live acceptance references | pending (local synthetic `DaprPolicyConformanceTests` / `WorkersHostSecurityEnrollmentTests` only; live acceptance pending) |
| Ordinary public CLI qualification / registration acceptance references | pending |
| Builds catalog gitlink / release tag commit | `bf8a28b4bbcec619e8d9764d335f786f2bafb69e` (`v6.8.1`) |
| Tool package / exact restored version / pin comparison | `hexalith.builds.module.cli` / `6.9.0` in Folders `.config/dotnet-tools.json` vs `6.6.2` in direct Platform `.config/dotnet-tools.json` (mismatch — pending alignment) |
| Verifier / owner / UTC date | pending |
| Environment / OS / SDK / Aspire / Dapr identity | pending |
| Command / working directory / output mode / exit | pending |
| Environment ID / run ID / mapping path and hash | pending |
| Loaded project/package identities and Debug configuration | pending |
| Human source audit: dependency selectors, helpers, unsupported/generated constructs | pending (`hexalith-module validate --adoption` currently reports `dependency-selectors` findings in root props and tracked evidence copies) |
| Human Dapr audit: injected names, declarative routes/dead-letter, ACLs and mTLS | pending (`hexalith-module validate --adoption` currently reports `dapr-subscriptions` findings for programmatic `MapSubscribeHandler`/`WithTopic` and literal component names) |
| Breakpoint: active file / line / process / loaded assembly / hit evidence | pending |
| Active-checkout edit / rebuild or reload / observed runtime effect | pending |
| Direct root .gitmodules / recorded gitlinks / checkout HEAD comparison | Observed clean direct-only checkouts (`PASS references`, `PASS gitlinks`, `PASS nested-references`); re-verify on final SM-2 run |
| Direct-only initialization command / before and after state | pending |
| Initialized nested reference check / result | pending (static `PASS nested-references`; re-verify during SM-2 run) |
| Hosting inventory / freeze / gated retirement review | `module/hosting-inventory.json` inventories `Hexalith.Folders.AppHost`, `Hexalith.Folders.Aspire`, and `Hexalith.Folders.ServiceDefaults` as `frozen` with pending retirement gates |
| Readiness / tasks / duration / deadline / diagnostic files | pending |
| Down command / run identity / cleanup outcome | pending |
| Result / failures / corrective action / evidence links | pending |
| Story 3.11 evidence reference / owner acceptance | pending |

- [ ] Owner reviewed every module-owned source/build/helper path, including constructs outside the static scanner's supported patterns.
- [ ] Tool pin equals the direct Platform checkout; published release and gitlink identities verified.
- [ ] Accountable module and Platform owners reviewed the registration handoff before ordinary CLI run/debug; its direct Platform revision contains the required executable bindings, scoped configuration, authentication/token adapters, gateway routing and Dapr policies.
- [ ] Composition-only synthetic observations remain separate from ordinary CLI qualification and real-owner/live acceptance; the historical Dapr timeout stays unresolved without new live evidence.
- [ ] Breakpoint hit and source-edit runtime effect observed in the active checkout.
- [ ] Direct-only/nested checks captured; no sibling or nested fallback selected.
- [ ] No hosting project retired without producer adoption, owner-reviewed source/package/deployed parity and applicable EventStore AD-22 exact-subject removal authority.

Result: **pending / SM-2 unverified**. Never infer completion from static PASS rows.
