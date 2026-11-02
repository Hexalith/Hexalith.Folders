# Local catalog composition

The Folders AppHost supports the SDK's private Development catalog bootstrap. Build the conventional
Debug project-reference outputs before starting it, including the independently composed Tenants and
Memories hosts. Keep `UseHexalithProjectReferences=true`, `UseNuGetDeps=false` and the existing SDK
assembly version consistent across every build.

For a retained local backend, configure `Folders:DevelopmentCatalog:CustodyDirectory` to an absolute
private directory outside the checkout. The AppHost supplies its existing persisted
`folders-idempotency-digest-key`; restore both the directory and that retained key when restoring
business state. An empty catalog cell does not authorize replacement digest material. The SDK retains
committed catalog history and refuses changed backend, key-ring or predecessor identities.

The composition binds the final selected sidecar AppIds, actual compiled Folders/Tenants owners,
existing adapters, shared authority component and closed receiver ACLs. Gateway, Folders, Tenants,
Memories and each Workers process observe and activate their own host binding through the supported
SDK lifecycle. Configuration or catalog JSON alone does not establish readiness. Catalog controls,
workload assertions, human receipts and app-channel credentials retain their separate checks.

The disposable durability fixture composes after selecting its new owned Redis backend and final
isolated identities. It provisions its private digest ring with that backend, observes every physical
replica, and binds direct workload test requests to actual committed receiver readbacks. This fixture
profile grants no production or public-v2 authority. Failed activation retains SDK recovery inputs;
never delete another run's custody to clear a readiness failure.

The declared adapter migration preserves the original canonical payload bytes, semantic option hash,
nullable fields, operation/adapter/version identities, task and credential scope, and mutation/commit
retention. The fixed legacy corpus and real SDK component authority tests cover those compatibility
properties; real-provider runtime qualification is reported separately in the Story 12.2 evidence.
