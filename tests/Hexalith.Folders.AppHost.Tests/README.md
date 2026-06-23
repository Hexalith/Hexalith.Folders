# Hexalith.Folders.AppHost.Tests

Tier-3 Aspire integration harness. It boots the full Folders `AppHost` topology through
`DistributedApplicationTestingBuilder` and proves the cross-process publish/subscribe wiring comes up together:

- `eventstore` publishes managed-tenant folder domain events to the single `folders.events` topic (Story 10.3 D1
  `EventStore:Publisher:TopicOverrides:folders` override).
- `folders-workers` subscribes `folders.events` on `/folders/events` (semantic indexing) and `memories-events`.
- `memories` subscribes `memories-events` (the curated search index).

## Running the harness

These tests require a **DCP-capable host**: Docker running plus a Dapr runtime (`dapr init`). They are **opt-in**
and skip everywhere else so the hermetic full-solution lane stays green without infrastructure:

```bash
HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true \
  dotnet test tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj
```

Without the variable set, `AspireFoldersAppHostFixture` reports unavailable and every test calls
`SkipIfUnavailable()`. The wider environment currently has a known Aspire CLI/DCP boot mismatch (Epic 9 residual);
standing up the dedicated DCP lane closes the live-boot evidence this harness produces.

## Extending

`AspireFoldersAppHostFixture` is reusable. The deeper folder-mutation → worker-receipt assertion (drive a folder
mutation through the `folders` gateway, then observe the worker's bridge status) layers on the same fixture. It is
currently moot end-to-end because the production content materializer is fail-closed (Story 10.3 Task 4) until a
real workspace/provider content reader is wired.
