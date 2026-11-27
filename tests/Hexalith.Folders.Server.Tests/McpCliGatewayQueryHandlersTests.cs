using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Folders.Aggregates.Organization;
using Hexalith.Folders.Queries.Folders;
using Hexalith.Folders.Queries.Gateway;
using Hexalith.Folders.Queries.ProviderReadiness;
using Shouldly;
using Xunit;

using static Hexalith.Folders.Server.Tests.McpCliGatewayQueryFixture;

namespace Hexalith.Folders.Server.Tests;

/// <summary>Qualifies the signed public Gateway queries against production handlers and persisted source projections.</summary>
public sealed class McpCliGatewayQueryHandlersTests
{
    /// <summary>Checks registered operations, deny-before-access, paging, freshness, scope and redaction over HTTP.</summary>
    [Fact]
    public async Task SignedCallbacksServeOnlyAuthorizedDurableMetadata()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var fixture = await McpCliGatewayQueryFixture.CreateAsync(token).ConfigureAwait(true);
        await using var fixtureLifetime = fixture.ConfigureAwait(true);
        string actor = ProviderBindingActorId.Create(ProviderBinding);
        object folder = new { folderId = Folder };
        object workspace = new { folderId = Folder, workspaceId = Workspace };
        object taskWorkspace = new { folderId = Folder, workspaceId = Workspace, taskId = TaskId };
        object commit = new { folderId = Folder, workspaceId = Workspace, operationId = CommitOperation };
        object indexing = new { folderId = Folder, taskId = TaskId };
        object providerBinding = new { providerBindingRef = ProviderBinding };
        object readiness = new { providerBindingRef = ProviderBinding, requestedCapability = "repository_creation" };
        object search = new { folderId = Folder, workspaceId = Workspace, taskId = TaskId, queryFamily = "semantic_reference_pending", queryText = "private raw query" };

        (QueryEnvelope Query, string Projection, string Freshness, string Field, string? Value)[] cases =
        [
            (Query("GetBranchRefPolicy", "read_branch_ref_policy", folder), "get-branch-ref-policy", "eventually_consistent", "policyRef", "branch_policy_gateway"),
            (Query("GetCommitEvidence", "query_status", commit), "get-commit-evidence", "eventually_consistent", "operationId", CommitOperation),
            (Query("GetEffectivePermissions", "read_metadata", indexing), "get-effective-permissions", "read_your_writes", "folderId", Folder),
            (Query("GetFolderIndexingStatus", "read_context_search", indexing), "get-folder-indexing-status", "eventually_consistent", "items", null),
            (Query("GetFolderLifecycleStatus", "read_metadata", folder), "get-folder-lifecycle-status", "eventually_consistent", "folderId", Folder),
            (Query("GetProviderBinding", "tenant-context-and-provider-binding-read", providerBinding, actor), "get-provider-binding", "eventually_consistent", "providerBindingRef", ProviderBinding),
            (Query("GetProviderOutcome", "query_status", commit), "get-provider-outcome", "eventually_consistent", "operationId", CommitOperation),
            (Query("GetReconciliationStatus", "query_status", new { folderId = Folder, workspaceId = Workspace, reconciliationId = Reconciliation }),
                "get-reconciliation-status", "eventually_consistent", "reconciliationId", Reconciliation),
            (Query("GetRepositoryBinding", "read_metadata", new { folderId = Folder, repositoryBindingId = Binding }),
                "get-repository-binding", "eventually_consistent", "repositoryBindingId", Binding),
            (Query("GetTaskStatus", "query_status", indexing), "get-task-status", "eventually_consistent", "taskId", TaskId),
            (Query("GetWorkspaceCleanupStatus", "read_workspace_cleanup_status", taskWorkspace),
                "get-workspace-cleanup-status", "read_your_writes", "correlationId", Correlation),
            (Query("GetWorkspaceLock", "read_workspace_lock", workspace), "get-workspace-lock", "read_your_writes", "lockState", "locked"),
            (Query("GetWorkspaceRetryEligibility", "read_workspace_status", taskWorkspace),
                "get-workspace-retry-eligibility", "eventually_consistent", "taskId", TaskId),
            (Query("GetWorkspaceStatus", "read_workspace_status", workspace) with { EntityId = Workspace },
                "get-workspace-status", "read_your_writes", "workspaceId", Workspace),
            (Query("ListFolderAclEntries", "manage_folder_access", folder), "list-folder-acl-entries", "eventually_consistent", "items", null),
            (Query("SearchFolderIndexedFiles", "read_context_search", search), "search-folder-indexed-files", "eventually_consistent", "items", null),
            (Query("ValidateProviderReadiness", "create_folder", readiness, actor), "validate-provider-readiness", "snapshot_per_task", "providerBindingRef", ProviderBinding),
        ];
        foreach (var item in cases)
        {
            QueryResult result = await SuccessAsync(fixture, item.Query, token);
            result.ProjectionType.ShouldBe(item.Projection);
            result.Metadata.ShouldNotBeNull().Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
            result.Metadata.IsStale.ShouldBe(false);
            JsonElement payload = result.GetPayload();
            payload.GetProperty("freshness").GetProperty("readConsistency").GetString().ShouldBe(item.Freshness);
            JsonElement field = payload.GetProperty(item.Field);
            if (item.Value is not null)
            {
                field.GetString().ShouldBe(item.Value);
            }
            else
            {
                field.GetArrayLength().ShouldBeGreaterThan(0);
            }

            string json = payload.GetRawText();
            json.ShouldNotContain(Credential);
            json.ShouldNotContain("credentialReferenceId");
            json.ShouldNotContain("foreign-private-file");
            json.ShouldNotContain("private raw query");
        }

        var commitEvidence = await SuccessAsync(fixture, cases[1].Query with { EntityId = Workspace }, token);
        commitEvidence.GetPayload().GetProperty("changedPathMetadataDigest").GetString().ShouldBe("digest_path_metadata");
        var workspaceStatus = await SuccessAsync(fixture, cases[13].Query, token);
        JsonElement outcome = workspaceStatus.GetPayload().GetProperty("providerOutcome");
        outcome.TryGetProperty("changedPathMetadataDigest", out _).ShouldBeFalse();
        outcome.TryGetProperty("commitReferenceClassification", out _).ShouldBeFalse();
        outcome.TryGetProperty("reconciliationReference", out _).ShouldBeFalse();

        var historicalTask = await SuccessAsync(fixture, Query("GetTaskStatus", "query_status",
            new { folderId = Folder, taskId = HistoricalTaskId }) with { EntityId = HistoricalTaskId }, token);
        historicalTask.GetPayload().GetProperty("taskId").GetString().ShouldBe(HistoricalTaskId);

        string tenantBindingActor = ProviderBindingActorId.Create(Tenant);
        foreach (QueryEnvelope providerRead in new[]
        {
            Query("GetProviderBinding", "tenant-context-and-provider-binding-read", new { providerBindingRef = Tenant }, tenantBindingActor),
            Query("ValidateProviderReadiness", "create_folder", new { providerBindingRef = Tenant, requestedCapability = "repository_creation" }, tenantBindingActor),
        })
        {
            (await SuccessAsync(fixture, providerRead, token)).GetPayload().GetProperty("providerBindingRef").GetString().ShouldBe(Tenant);
        }

        var unscoped = await SuccessAsync(fixture, Query("GetEffectivePermissions", "read_metadata", folder), token);
        unscoped.GetPayload().GetProperty("permissions").EnumerateArray().Select(level => level.GetString()).ShouldBe(["read", "administer"]);
        var mismatchedAction = await SuccessAsync(fixture, cases[2].Query with
        {
            CallerAuthority = new("authenticated-caller", [], ["read_metadata", "archive_folder"]),
        }, token);
        mismatchedAction.GetPayload().GetProperty("permissions").EnumerateArray().Select(level => level.GetString()).ShouldBe(["read"]);
        var matchingAction = await SuccessAsync(fixture, cases[2].Query with
        {
            CallerAuthority = new("authenticated-caller", [], ["read_metadata", "manage_folder_access"]),
        }, token);
        matchingAction.GetPayload().GetProperty("permissions").EnumerateArray().Select(level => level.GetString()).ShouldBe(["read", "administer"]);

        QueryEnvelope lifecycle = cases[4].Query;
        QueryEnvelope searchQuery = cases[15].Query;
        QueryEnvelope providerQuery = cases[5].Query;
        int searchCalls = fixture.SearchCalls;
        int providerCalls = fixture.ProviderCalls;
        fixture.Store.Reads.Clear();
        string denied = await DenialAsync(fixture, lifecycle with { CallerAuthority = null }, token, sign: false);
        (await DenialAsync(fixture, lifecycle with { CallerAuthority = null }, token, sign: false, withIdempotency: true)).ShouldBe(denied);
        foreach (QueryEnvelope delegated in new[] { lifecycle, searchQuery, providerQuery })
        {
            (await DenialAsync(fixture, delegated with { IsDelegated = true, DelegationId = OtherUser }, token)).ShouldBe(denied);
        }
        (await DenialAsync(fixture, providerQuery with { DelegationId = OtherUser }, token)).ShouldBe(denied);
        (await DenialAsync(fixture, cases[16].Query with
        {
            CallerAuthority = new("authenticated-caller", [], ["provider_readiness_read"]),
        }, token)).ShouldBe(denied);
        fixture.Store.Reads.ShouldBeEmpty();
        foreach (QueryEnvelope input in new[]
        {
            lifecycle with { UserId = "forged-user", OriginalActorId = "forged-user" },
            searchQuery,
            providerQuery,
            lifecycle,
        })
        {
            QueryEnvelope foreign = input with { TenantId = "other-tenant" };
            (await DenialAsync(fixture, input, token, signed: foreign)).ShouldBe(denied);
        }
        (await DenialAsync(fixture, lifecycle with { UserId = RevokedUser, OriginalActorId = RevokedUser }, token)).ShouldBe(denied);
        (await DenialAsync(fixture, lifecycle with { CallerAuthority = new("authenticated-caller", [], ["write_only"]) }, token)).ShouldBe(denied);
        (await DenialAsync(fixture, Query("GetFolderLifecycleStatus", "read_metadata", new { folderId = "opaque_MissingFolder" }, "opaque_MissingFolder"), token)).ShouldBe(denied);
        fixture.Store.Reads.ShouldAllBe(key => key.StartsWith("projection:folders:tenant-access:", StringComparison.Ordinal)
            || key.StartsWith("projection:folders:folder-permissions:", StringComparison.Ordinal)
            || key.StartsWith("projection:folders:organization-permissions:", StringComparison.Ordinal));
        fixture.SearchCalls.ShouldBe(searchCalls);
        fixture.ProviderCalls.ShouldBe(providerCalls);

        foreach (QueryEnvelope invalid in new[]
        {
            lifecycle with { Payload = """{"folderId":null}"""u8.ToArray() },
            lifecycle with { Payload = JsonSerializer.SerializeToUtf8Bytes(new { folderId = Folder, idempotencyKey = Correlation }) },
            lifecycle with { Payload = JsonSerializer.SerializeToUtf8Bytes(new { folderId = Folder, unsupported = true }) },
            lifecycle with { Payload = JsonSerializer.SerializeToUtf8Bytes(new { folderId = Folder, freshness = "read_your_writes" }) },
            lifecycle with { Paging = new() },
            Query("GetTaskStatus", "query_status", folder),
            Query("GetTaskStatus", "query_status", new { folderId = Folder, taskId = "short" }),
            providerQuery with { AggregateId = Tenant },
            providerQuery with { AggregateId = ProviderBinding },
            providerQuery with { AggregateId = ProviderBindingActorId.Create("opaque_OtherProvider") },
            cases[16].Query with { AggregateId = Tenant },
            cases[16].Query with { Payload = JsonSerializer.SerializeToUtf8Bytes(new { providerBindingRef = ProviderBinding, requestedCapability = 0 }) },
        })
        {
            fixture.Store.Reads.Clear();
            await FailureAsync(fixture, invalid, HttpStatusCode.BadRequest, QueryAdapterFailureReason.InvalidEnvelope, token);
            fixture.Store.Reads.ShouldBeEmpty();
        }
        await FailureAsync(fixture, lifecycle, HttpStatusCode.BadRequest, QueryAdapterFailureReason.InvalidEnvelope, token, withIdempotency: true);
        fixture.Store.Reads.ShouldBeEmpty();

        (await DenialAsync(fixture, Query("GetFolderIndexingStatus", "read_context_search",
            new { folderId = Folder, taskId = "opaque_ForeignTask00" }), token)).ShouldBe(denied);
        (await DenialAsync(fixture, Query("GetWorkspaceCleanupStatus", "read_workspace_cleanup_status",
            new { folderId = Folder, workspaceId = "opaque_ForeignWorkspace00", taskId = TaskId }), token)).ShouldBe(denied);
        (await SuccessAsync(fixture, Query("GetTaskStatus", "query_status",
            new { folderId = OtherFolder, taskId = ForeignTaskId }, OtherFolder), token)).GetPayload()
            .GetProperty("taskId").GetString().ShouldBe(ForeignTaskId);
        (await DenialAsync(fixture, Query("GetTaskStatus", "query_status",
            new { folderId = Folder, taskId = ForeignTaskId }), token)).ShouldBe(denied);

        (await SuccessAsync(fixture, Query("GetFolderIndexingStatus", "read_context_search", folder, user: OtherUser), token))
            .GetPayload().GetProperty("items").GetArrayLength().ShouldBeGreaterThan(0);
        fixture.Store.Reads.Clear();
        (await DenialAsync(fixture, Query("GetFolderIndexingStatus", "read_context_search", indexing, user: OtherUser), token)).ShouldBe(denied);
        fixture.Store.Reads.ShouldNotContain(key => key.Contains(":semantic-indexing:", StringComparison.Ordinal));
        (await DenialAsync(fixture, Query("SearchFolderIndexedFiles", "read_context_search", new
        {
            folderId = Folder, workspaceId = "opaque_ForeignWorkspace00", queryFamily = "semantic_reference_pending", queryText = "needle",
        }), token)).ShouldBe(denied);

        string missingBinding = "opaque_MissingProvider";
        int evidenceCount = fixture.ReadinessEvidenceCount;
        (await DenialAsync(fixture, Query("ValidateProviderReadiness", "create_folder",
            new { providerBindingRef = missingBinding, requestedCapability = "repository_creation" },
            ProviderBindingActorId.Create(missingBinding)), token)).ShouldBe(denied);
        fixture.ReadinessEvidenceCount.ShouldBe(evidenceCount);
        fixture.ProviderCalls.ShouldBe(providerCalls);
        fixture.SearchCalls.ShouldBe(searchCalls);

        QueryEnvelope aclPage = Query("ListFolderAclEntries", "manage_folder_access", new { folderId = Folder, limit = 1 });
        QueryResult aclFirst = await SuccessAsync(fixture, aclPage, token);
        var paging = aclFirst.Metadata.ShouldNotBeNull().Paging.ShouldNotBeNull();
        paging.PageSize.ShouldBe(1);
        paging.HasMore.ShouldBe(true);
        string cursor = paging.NextCursor.ShouldNotBeNull();
        QueryResult aclNext = await SuccessAsync(fixture,
            aclPage with { Paging = new(PageSize: 1, Cursor: cursor) }, token);
        aclNext.Metadata.ShouldNotBeNull().Paging.ShouldNotBeNull().NextCursor.ShouldNotBe(cursor);
        QueryResult aclMatch = await SuccessAsync(fixture, Query("ListFolderAclEntries", "manage_folder_access",
            new { folderId = Folder, cursor, limit = 1 }, paging: new(PageSize: 1, Cursor: cursor)), token);
        aclMatch.GetPayload().GetProperty("items").GetRawText().ShouldBe(aclNext.GetPayload().GetProperty("items").GetRawText());

        QueryResult searchPage = await SuccessAsync(fixture, searchQuery with { Paging = new(PageSize: 1) }, token);
        searchPage.GetPayload().GetProperty("items")[0].GetProperty("fileVersionReference").GetString().ShouldBe(fixture.FileVersion);
        var searchPaging = searchPage.Metadata.ShouldNotBeNull().Paging.ShouldNotBeNull();
        searchPaging.PageSize.ShouldBe(1);
        searchPaging.NextCursor.ShouldBeNull();
        searchPaging.HasMore.ShouldBe(false);
        fixture.SetSearchTail(foreign: false, visible: false);
        QueryResult singleMatch = await SuccessAsync(fixture, searchQuery with { Paging = new(PageSize: 1) }, token);
        searchPage.GetPayload().GetProperty("limits").GetRawText().ShouldBe(singleMatch.GetPayload().GetProperty("limits").GetRawText());
        searchPage.Metadata.Paging.ShouldBe(singleMatch.Metadata.ShouldNotBeNull().Paging);

        fixture.SetSearchTail(foreign: true, visible: true);
        QueryResult visibleSearchPage = await SuccessAsync(fixture, searchQuery with { Paging = new(PageSize: 1) }, token);
        searchPaging = visibleSearchPage.Metadata.ShouldNotBeNull().Paging.ShouldNotBeNull();
        searchPaging.NextCursor.ShouldNotBeNull();
        searchPaging.HasMore.ShouldBe(true);
        QueryResult searchNext = await SuccessAsync(fixture, searchQuery with { Paging = new(PageSize: 1, Cursor: searchPaging.NextCursor) }, token);
        searchNext.GetPayload().GetProperty("items")[0].GetProperty("fileVersionReference").GetString().ShouldBe(fixture.NextFileVersion);
        searchNext.Metadata.ShouldNotBeNull().Paging.ShouldNotBeNull().HasMore.ShouldBe(false);
        QueryEnvelope typedSearchPage = Query("SearchFolderIndexedFiles", "read_context_search", new
        {
            folderId = Folder, workspaceId = Workspace, taskId = TaskId, queryFamily = "semantic_reference_pending",
            queryText = "private raw query", limit = 1, cursor = searchPaging.NextCursor,
        });
        QueryResult equalSearchPage = await SuccessAsync(fixture, typedSearchPage with
        {
            Paging = new(PageSize: 1, Cursor: searchPaging.NextCursor),
        }, token);
        equalSearchPage.GetPayload().GetProperty("items").GetRawText()
            .ShouldBe(searchNext.GetPayload().GetProperty("items").GetRawText());

        foreach ((QueryEnvelope input, string reason) in new (QueryEnvelope, string)[]
        {
            (aclPage with { Paging = new(PageSize: 2) }, QueryAdapterFailureReason.InvalidEnvelope),
            (aclPage with { Paging = new(Offset: 0) }, QueryAdapterFailureReason.InvalidEnvelope),
            (aclPage with { Paging = new(PageSize: 0) }, QueryAdapterFailureReason.InvalidEnvelope),
            (cases[14].Query with { Paging = new(PageSize: 1001) }, QueryAdapterFailureReason.InvalidEnvelope),
            (Query("ListFolderAclEntries", "manage_folder_access", new { folderId = Folder, filter = "subject=user" }), QueryAdapterFailureReason.InvalidEnvelope),
            (Query("ListFolderAclEntries", "manage_folder_access", new { folderId = Folder, cursor }, paging: new(Cursor: "conflict")), QueryAdapterFailureReason.InvalidCursor),
            (searchQuery with { Paging = new(Offset: 0) }, QueryAdapterFailureReason.InvalidEnvelope),
            (searchQuery with { Paging = new(PageSize: 501) }, QueryAdapterFailureReason.InvalidEnvelope),
            (searchQuery with { Paging = new(Cursor: "tampered") }, QueryAdapterFailureReason.InvalidCursor),
            (typedSearchPage with { Paging = new(PageSize: 2) }, QueryAdapterFailureReason.InvalidEnvelope),
            (typedSearchPage with { Paging = new(Cursor: "memories-search:3") }, QueryAdapterFailureReason.InvalidCursor),
        })
        {
            fixture.Store.Reads.Clear();
            await FailureAsync(fixture, input, HttpStatusCode.BadRequest, reason, token);
            fixture.Store.Reads.ShouldBeEmpty();
        }

        string unavailable = QueryAdapterFailureReason.ReadModelUnavailable;
        fixture.Source.Heads[(Tenant, actor)]++;
        await FailureAsync(fixture, providerQuery, HttpStatusCode.OK, unavailable, token);
        fixture.Source.Heads[(Tenant, actor)]--;
        fixture.Source.Heads[(Tenant, Folder)]++;
        await FailureAsync(fixture, cases[0].Query, HttpStatusCode.OK, unavailable, token);
        fixture.Source.Heads[(Tenant, Folder)]--;
        await fixture.SetTenantFreshnessAsync(stale: true, token);
        await FailureAsync(fixture, lifecycle, HttpStatusCode.OK, unavailable, token);
        await fixture.SetTenantFreshnessAsync(stale: false, token);
        fixture.Store.UnavailableKey = EventStoreProviderReadinessBindingReadModel.Key(Tenant, actor);
        await FailureAsync(fixture, providerQuery, HttpStatusCode.OK, unavailable, token);
        fixture.Store.UnavailableKey = "tenant-access";
        await FailureAsync(fixture, lifecycle, HttpStatusCode.OK, unavailable, token);
    }

    /// <summary>Checks nested metadata scrubbing independently of each domain DTO's allowlist.</summary>
    [Fact]
    public void ResponseRedactionRemovesProtectedFieldsAtAnyDepth()
    {
        QueryResult result = GatewayQueryExecutionSupport.Success(new
        {
            items = new[]
            {
                new
                {
                    inlineContent = "fixture-body", contentBytes = "fixture-bytes", streamDescriptor = new { id = "fixture-stream" },
                    rawPath = "fixture-path", credentialRef = "fixture-credential", secret = "fixture-secret", fileVersionRef = "safe-opaque-handle",
                }
            },
        }, "search-folder-indexed-files", "eventually_consistent",
            new FolderLifecycleFreshness("eventually_consistent", DateTimeOffset.UtcNow, "2", false, null));
        JsonElement item = result.GetPayload().GetProperty("items")[0];
        item.EnumerateObject().Select(p => p.Name).ShouldBe(["fileVersionRef"]);
    }

    private static async Task<QueryResult> SuccessAsync(McpCliGatewayQueryFixture fixture, QueryEnvelope input, CancellationToken token)
    {
        using HttpResponseMessage response = await fixture.PostAsync(input, token).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, input.QueryType);
        QueryResult result = (await response.Content.ReadFromJsonAsync<QueryResult>(token).ConfigureAwait(true)).ShouldNotBeNull();
        result.Success.ShouldBeTrue(input.QueryType + " | " + result.ErrorMessage);
        return result;
    }

    private static async Task<string> DenialAsync(McpCliGatewayQueryFixture fixture, QueryEnvelope input, CancellationToken token,
        QueryEnvelope? signed = null, bool sign = true, bool withIdempotency = false)
    {
        using HttpResponseMessage response = await fixture.PostAsync(input, token, signed, sign, withIdempotency).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, input.QueryType);
        (await response.Content.ReadFromJsonAsync<QueryResult>(token).ConfigureAwait(true)).ShouldBe(QueryResult.Failure("safe-denial"));
        return await response.Content.ReadAsStringAsync(token).ConfigureAwait(true);
    }

    private static async Task FailureAsync(McpCliGatewayQueryFixture fixture, QueryEnvelope input, HttpStatusCode status,
        string reason, CancellationToken token, bool withIdempotency = false)
    {
        using HttpResponseMessage response = await fixture.PostAsync(input, token, withIdempotency: withIdempotency).ConfigureAwait(true);
        response.StatusCode.ShouldBe(status, input.QueryType);
        (await response.Content.ReadFromJsonAsync<QueryResult>(token).ConfigureAwait(true)).ShouldBe(QueryResult.Failure(reason));
    }
}
