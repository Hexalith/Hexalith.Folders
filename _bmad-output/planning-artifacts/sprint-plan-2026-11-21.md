# Folders sprint plan · 21 November 2026

**The sprint goal is passing package-mode Release CI on one identifiable Folders commit.** The Release solution and the three affected test projects now pass locally with the refreshed dependency revisions. The release maintainer owns the remaining push CI and contract-spine evidence. [Course correction and test results](sprint-change-proposal-2026-11-21.md)

## Delivery order

| Order | Owner | Completion evidence |
| --- | --- | --- |
| 1 | Release, Builds and Folders AppHost maintainers | Record green CI and contract-spine runs for the commit containing the fixes and updated gitlinks. The run on `73ca79b4` had 12 failing Contracts cases and one failing packaged AppHost case. [CI run](https://github.com/Hexalith/Hexalith.Folders/actions/runs/44873203413) · [Contract-spine run](https://github.com/Hexalith/Hexalith.Folders/actions/runs/44873203186) |
| 2 | Platform and Folders fixture maintainers | Bind the selected Platform gitlink and component evidence to the Folders fixture. Qualify on those inputs and address the SM4 latency overruns. The November 15 receipts identify the previous passing inputs. [Reviewed fixture results](../implementation-artifacts/spec-12-2-historical-source-authority.md) |
| 3, after the prerequisites below | Folders provider maintainer | Exercise Story 12.4 on the disposable Forgejo repository Jerome selected. Use the normally registered executor and qualified transport. Capture the remote ref, exact bytes, single logical commit and terminal task after interruption and resume. [Story 12.4](epics.md#story-124-real-git-commit-executor-and-provider-write-path) |

The SDK isolation action is complete. Normal and interrupted generation with concurrent readers left the tracked client byte-identical. The SDK and documentation maintainers still own the supported v1 quickstart and the sample's lock release and reacquisition on an authorized host. CiCd owns the shared Dependency Review and CodeQL workflow review. [Maintenance actions](../implementation-artifacts/sprint-status.yaml)

## Readiness and dependencies

The BMAD readiness verdict is **CONCERNS**. Local Release verification is green. Product scheduling depends on the following evidence.

- **Story 12.2.** Platform `0aacf132` records Story 2.12 done. The reviewed November 15 Folders fixture passed bounded history, operational durability and folder durability. The selected Platform gitlink still needs matching Folders evidence. SM4 performance, release and governance gates are open. [Platform tracker](../../references/Hexalith.Platform/_bmad-output/implementation-artifacts/sprint-status.yaml) · [Folders qualification](../implementation-artifacts/spec-12-2-historical-source-authority.md)
- **McpCli.** At `8042275f`, inventory Story 5.8 is done. Enrollment 6.1 and shared-head parity 6.2 are in backlog. Folders enrollment and retirement both await their own acceptance evidence. [McpCli tracker](../../references/Hexalith.McpCli/_bmad-output/implementation-artifacts/sprint-status.yaml)
- **Stories 12.4 and 12.5.** Both depend on 12.1, 12.2, 12.3 and 12.7. Story 12.1 is done, 12.2 and 12.3 are in progress, and 12.7 is in backlog. After an uncertain provider write, preserve the operation identity and fence, suspend writes, and make up to five read-only checks within 15 minutes. Conflicting or exhausted evidence requires reconciliation. Story 12.5 must recover both an index change and a removal after refusal and restart. [Story definitions](epics.md) · [Execution manifest](planning-story-manifest.yaml)
- **Story 1.17.** Operational entry and closure require host artifacts, staging and consumer evidence, and an owner decision. The recorded deployment mode is `V1Only`. [Closure handoff](../implementation-artifacts/story-1-17-closure-owner-intake-2026-11-16.md)

## Tracker verification

The parent checkout matched fetched `origin/main` at `73ca79b4`. All 13 direct submodules fast-forwarded to their fetched `main` revisions. The parent still records the earlier gitlinks until the updated revisions are committed.

The epic definitions, execution manifest and tracker agree on 163 stories across 13 epics and workstreams. **115 stories are done, 5 in progress and 43 in backlog.** The five active stories are 5.8, 10.8, 12.2, 12.3 and 12.6. All 285 prerequisite edges satisfy the manifest's rank or accepted-terminal rules.

The repaired BMAD generator preserved all 189 tracker rows, including epics and retrospectives, in a preview and a write to a scratch copy. The five title mismatches for 1.1, 5.8, 5.10, 10.1 and 10.9 now resolve to their recorded keys. The live tracker received a scoped maintenance update under the manifest's existing regeneration policy. [Sprint tracker](../implementation-artifacts/sprint-status.yaml) · [Execution manifest](planning-story-manifest.yaml)
