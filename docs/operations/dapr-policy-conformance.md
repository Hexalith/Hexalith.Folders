# Dapr Policy Conformance Handoff

Hexalith.Folders keeps two Dapr policy tracks:

- Local AppHost development uses `src/Hexalith.Folders.AppHost/DaprComponents/accesscontrol.yaml`. It is explicitly local-only and remains permissive with `spec.accessControl.defaultAction: allow` so developer sidecars can start without production dependencies.
- Production conformance uses sanitized repository artifacts under `deploy/dapr/production/`. These files are non-secret evidence for platform promotion and CI validation; deployment tooling may template namespace and trust-domain values before applying equivalent policy in the production operations repository.

## Production Artifacts

- `deploy/dapr/production/accesscontrol.yaml` contains per-target Dapr `Configuration` documents for the stable app IDs `eventstore`, `tenants`, `folders`, `folders-workers`, and `folders-ui`. Every target defaults to deny. The only current service-invocation allow rules are `eventstore` calling the `folders` sidecar on `POST /process` and `POST /project`.
- `deploy/dapr/production/daprsystem.yaml` captures the control-plane mTLS requirement with `spec.mtls.enabled: true` and bounded certificate timing settings. It intentionally contains no certificates, tokens, or secret material.
- `tests/fixtures/dapr-policy-conformance.yaml` maps allow rules to synthetic positive and negative cases. The fixture includes a normalized semantic hash of the production access-control policy so policy changes fail unless conformance cases are updated in the same change.

## Gate Behavior

Run the metadata-only conformance gate locally:

```powershell
./tests/tools/run-dapr-policy-conformance-gates.ps1
```

If restore and build already ran, use:

```powershell
./tests/tools/run-dapr-policy-conformance-gates.ps1 -SkipRestoreBuild
```

The gate validates:

- production access-control policy shape is fail-closed;
- every target uses deny-by-default and the synthetic production trust domain and namespace;
- no wildcard allow operations or wildcard HTTP verbs are introduced;
- production mTLS evidence is separate from service-invocation policy and enabled;
- the local AppHost policy remains local-only and permissive;
- negative controls cover unknown source app, known unauthorized source app, wrong target app, wrong operation, wrong HTTP verb, wrong namespace, and wrong trust domain;
- diagnostics and generated reports stay metadata-only.

The focused CI workflow runs this script with `actions/checkout` configured as `submodules: false` and without production secrets or endpoints.

## Promotion Notes

Platform operators should mirror or promote the sanitized repository policy into the production operations repository, applying environment-specific namespace and trust-domain templating there. Any additional service-invocation allow rule must be added to the production policy, the conformance fixture, and the negative controls together.

The live Dapr/kind execution gate that asserts actual denied invocation returns HTTP `403` remains a promotion or scheduled validation lane for Story 7.8. This story supplies deterministic static conformance in `tests/Hexalith.Folders.Contracts.Tests/OpenApi/DaprPolicyConformanceTests.cs` for the pull-request gate and records the live gate as `reference_pending_story_7_8` in `_bmad-output/gates/dapr-policy-conformance/latest.json`.
