---
title: "Product Brief: Hexalith.Folders"
status: "complete"
created: "2026-05-05T19:17:14+02:00"
updated: "2026-05-05T19:22:48+02:00"
inputs:
  - "_bmad-output/brainstorming/brainstorming-session-20260505-070846.md"
  - "_bmad-output/planning-artifacts/research/technical-hexalith-tenants-integration-for-folder-management-application-research-2026-05-05.md"
  - "_bmad-output/planning-artifacts/research/technical-hexalith-eventstore-domain-aggregates-research-2026-05-05.md"
  - "_bmad-output/planning-artifacts/research/technical-forgejo-and-github-api-research-2026-05-05.md"
---

# Product Brief: Hexalith.Folders

## Executive Summary

`Hexalith.Folders` is a tenant-scoped workspace storage module for chatbot developers building AI agents that need to create, read, modify, search, and persist project files reliably. It gives chatbots a simple task-oriented folder API while the module handles limited local storage, Git-backed repositories, tenant isolation, operational state, and event-based audit.

Today, chatbot developers are forced into brittle tradeoffs. They can let the bot touch the local filesystem directly, which creates isolation and recovery risks. They can embed Git automation inside chatbot logic, which couples agent workflows to provider-specific behavior. They can build one-off workspace state handling, which often fails when tasks are retried, interrupted, or partially completed. Or they can avoid persistence discipline entirely and lose the auditability needed for production use.

The core promise is straightforward: a chatbot can create a tenant-scoped workspace, create a new repository when the folder is Git-backed, write files, and commit them through a stable API. `Hexalith.Folders` owns the persistence boundary so chatbot developers can focus on agent behavior instead of filesystem safety, Git provider quirks, and cross-tenant data leakage.

## The Problem

AI chatbots increasingly behave like software collaborators. They generate project structures, update files, inspect codebases, and need durable workspace state across tasks. The storage layer behind those workflows is often improvised.

The current pain shows up in four ways:

- Ad hoc filesystem access makes it too easy to mix tenant data, leave partial task output behind, or lose track of which files belong to which workspace.
- Direct Git automation inside chatbot code forces every bot or orchestration layer to understand clone, branch, write, commit, push, provider auth, provider limits, and failure recovery.
- Tenant isolation is difficult to prove when workspace storage, folder permissions, Git credentials, and task metadata live in separate local conventions.
- Failed or interrupted tasks leave uncertain state: files may be written but not committed, repositories may be created but not indexed, and operators may not know whether a workspace can be trusted.

For chatbot developers, the cost is slower implementation, more duplicated infrastructure, weaker security boundaries, and less confidence that an AI task produced a recoverable and auditable result.

## The Solution

`Hexalith.Folders` provides an AI-native workspace boundary. Developers interact with tenant-scoped folders through commands and queries that match chatbot work: create or prepare a workspace, lock it for a task, write or move files, inspect the file tree, search or read relevant content, commit changes when Git-backed, and release the workspace.

The module supports Git-backed folders as the primary storage mode and local folders as a simpler, limited mode in the MVP. Local folders provide a lightweight persistence path for early or constrained workflows and can be upgraded to Git-backed folders when repository history, remote durability, or collaboration is needed. Git-backed folders add repository creation, commit history, collaboration readiness, and provider-backed persistence. In both modes, the chatbot uses the same conceptual API; storage mechanics remain inside the module.

The initial Git provider strategy covers GitHub and Forgejo through a capability-based provider abstraction. `Hexalith.Folders` should not treat Forgejo as a GitHub-compatible base URL swap. Provider differences such as authentication, repository provisioning, webhooks, limits, and version behavior are explicit capabilities validated through contract tests.

## What Makes This Different

`Hexalith.Folders` is not a generic file manager, not a Git UI, and not the chatbot interface. Its differentiated value is the narrow product boundary: reliable, tenant-isolated workspaces for AI agents.

Three design choices make the product fit that boundary:

- Task transaction workflow: a chatbot can prepare a workspace, acquire a task lock, make many file changes, then commit once with task metadata and correlation IDs.
- Event-first audit: folder, workspace, ACL, Git, and file operations emit metadata-only events so the system remains traceable without turning the event store into a blob store.
- Operations visibility: a read-only maintenance console shows workspace trust signals such as readiness, lock owner, uncommitted changes, last commit, failed operations, provider status, and sync state.

This approach keeps Git complexity out of chatbot code while still preserving enough operational truth for developers and maintainers to diagnose failures. The same operation model should be exposed through a .NET CLI console application and an MCP server so developers, automation, and AI tools can use the folder capabilities without depending on a single UI surface.

## Who This Serves

The primary user is the chatbot developer building agents that need reliable project workspaces. They need a stable API that lets their chatbot create repositories, write files, commit work, and retrieve context without manually managing every storage and Git edge case.

Secondary users are platform operators and maintainers. They need to see whether a workspace is ready, locked, dirty, failed, synced, or misconfigured without opening a file editor or becoming part of the chatbot user experience.

Tenant administrators are affected stakeholders. They need confidence that one tenant's folders, Git credentials, permissions, and task output cannot leak into another tenant's workspace.

## MVP Scope

The MVP should include:

- Tenant-scoped folder creation for Git-backed storage and limited local storage.
- Upgrade from limited local folder mode to Git-backed mode.
- Git-backed repository creation and commit support for GitHub and Forgejo.
- A chatbot-oriented task API: prepare, lock, write files, commit when Git-backed, and release.
- File tree, search, glob, metadata, and partial-read queries for AI context loading.
- Metadata-only events for folder lifecycle, file operations, workspace state, ACL changes, and Git commit references.
- Organization-owned Git credential references and explicit folder authorization checks.
- A .NET CLI console application exposing folder, workspace, file, Git, query, and status operations.
- An MCP server exposing the same core capabilities for AI tool and chatbot integration.
- Read-only operations visibility for workspace readiness, provider state, lock state, commit state, and failed operations.

Explicitly out of scope for the first version:

- End-user chatbot UI.
- File editing UI.
- Pull request, review, or branch workflow UI.
- Multi-remote repository mirroring.
- Full repair console or broad operator command surface.

## Success Criteria

The product is working when chatbot developers can persist task output without owning storage infrastructure. The first success signal is a complete task flow: create a tenant workspace, optionally start in limited local mode, upgrade or create Git-backed storage, create a repository, write files, commit changes, and query the resulting workspace state through the module API, .NET CLI, or MCP server.

Operational success should be measured by:

- Workspace task completion rate for prepare, lock, write, commit, and release flows.
- Cross-tenant authorization failures caught before file or repository access.
- Recovery clarity after interrupted tasks, including visible lock, dirty state, failed operation, and last successful commit.
- Provider portability demonstrated by contract-tested GitHub and Forgejo repository creation and commit behavior.
- Query efficiency for chatbot context loading, measured by use of file tree, metadata, search, glob, and partial reads instead of full workspace reads.

## Technical Approach

`Hexalith.Folders` should be implemented as a bounded context aligned with the existing Hexalith stack. `Hexalith.Tenants` remains the source of truth for tenant facts. `Hexalith.EventStore` remains the write-side command, aggregate, event, and projection foundation. Dapr provides pub/sub, service invocation, state, secret integration, and runtime resilience. Aspire composes the local development topology.

The operation surface should be available through three channels: service/API contracts for application integration, a .NET CLI console application for developer and operator automation, and an MCP server for AI tool integration. These surfaces should share the same command and query semantics so behavior, authorization, audit, and error handling stay consistent.

The write model should start with two aggregates: an organization aggregate for folder-specific organization settings, provider bindings, credential references, repository defaults, and ACL baselines; and a folder aggregate for folder lifecycle, storage mode, workspace readiness, ACL overrides, and compact file-operation facts. Git provisioning, workspace preparation, file synchronization, webhook handling, and retries belong in workers or process managers, not in aggregate handlers.

Read models should serve the chatbot and operations console directly. The chatbot needs efficient file context queries. Operators need trust and status projections. Neither should replay raw aggregate streams.

## Vision

If successful, `Hexalith.Folders` becomes the durable workspace substrate for Hexalith AI agents. It allows chatbots to work with files confidently across tenants, providers, and task lifecycles while preserving auditability and operational control.

Over time, the product can grow into a broader workspace reliability layer: richer provider support, brownfield folder adoption, repair workflows, drift detection, large-file policy enforcement, Git migration tooling, and deeper AI context indexing. The long-term opportunity is to make persistent AI workspaces dependable enough for production multi-tenant systems without forcing every chatbot team to rebuild the same storage, Git, and isolation infrastructure.
