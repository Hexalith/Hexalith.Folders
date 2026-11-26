# Draft for the Platform owner on Folders Story 12.2

Please explicitly **accept** or **reject** the run-owned workload authority prerequisite for Story 12.2 on these revisions. If you reject, please name the blocking condition.

- Folders [`a2f4dbcfe5c6a98e698e8fc5b1036e2f6d86b88a`](https://github.com/Hexalith/Hexalith.Folders/commit/a2f4dbcfe5c6a98e698e8fc5b1036e2f6d86b88a)
- Platform [`3848af49f9afc91b174b4c716751fe49cb15dbba`](https://github.com/Hexalith/Hexalith.Platform/commit/3848af49f9afc91b174b4c716751fe49cb15dbba)

Folders pins the Platform revision above. The November 26 live enrollment and issuer controls passed. Operational status durability, folder durability, and bounded history using the synthetic original-primary source each passed with zero failures or skips. All 1,299 observed runtime assembly bindings matched the 236 audited producer copies. The strict runs left zero observed owned processes, containers, or directories. [November 26 source qualification](evidence/story-12-2-current-source-qualification-20261126.json)

The standalone Platform fresh-host and cancellation controls passed on the same Platform pin with the Folders-selected source dependencies and the same provider JAR. Both left zero owned containers and temporary paths. [Platform enrollment and cancellation receipt](evidence/story-12-2-platform-owner-handoff-20261126.json)

This decision covers disposable source qualification for the Platform prerequisite. Story 12.2 stays in progress with SM4 open under its approved measurement-only scope. Publication, staging and deployed-consumer evidence, PD10/A6b governance, and independent whole-story acceptance remain open. [Story 12.2](spec-12-2-durable-projections-and-task-completion-pipeline.md#current-pinned-source-qualification--2026-11-26) · [External dependency tracker](../planning-artifacts/planning-story-manifest.yaml)
