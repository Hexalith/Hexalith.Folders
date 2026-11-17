# Complete Server qualification

The shared Release build prepares the .NET test outputs once. The preparation
script also builds Platform's Keycloak workload-binding provider before hooks.
`tests/tools/run-complete-project-tests.ps1` delegates the complete prebuilt
Server assembly to Platform's `verify_component.py --complete-assembly` once.
It enables all eleven real-issuer cases inside that fixture lifetime and uses
no class or method filter. Other project execution is unchanged.

The evaluated Server test build records the SDK identity authority. Debug source
mode requires exact source DLL SHA-256 and MVID equality. Package mode uses the
copied `sdk-binding.project.assets.json` and generated test `.deps.json` to
resolve one package ID, version and runtime asset for every checked SDK DLL. It
recomputes the actual NuGet archive's canonical content SHA-512 with the pinned
SDK's NuGet API, including signed-package record adjustments, against restore
content identity. It also binds raw archive SHA-512 to the restored sidecar,
checks manifest identity and compares the loaded DLL's SHA-256 and MVID to the
archive payload. Missing, ambiguous or changed metadata and payloads fail. File
availability never selects a different authority or permits source fallback.

Platform provisions a private disposable TLS issuer and naturally expired token,
performs independent RSA verification and awaits the child under a deadline.
Qualification requires one nonempty complete-project summary, native success,
zero error/failure/skip/not-run counters, and XML evidence for all eleven passing
cases. Cleanup observes the owned container and private directory on success and
failure. Unverified cleanup fails qualification. Diagnostics are bounded and
redact credentials and tokens.

These results prove disposable component behavior and package provenance. They
do not establish protected enrollment, satisfy held FR58 work, qualify AppHost's
multiple-cohort lifecycle, or complete migration acceptance. Publication still
requires the approved exact-input preparation checks and independent gates.
