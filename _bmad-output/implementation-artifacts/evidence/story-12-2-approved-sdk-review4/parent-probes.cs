using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

string probe = args[0];
using var client = new RecordingDaprClient { PersistTypedState = true };
var clock = new ProbeClock();
var options = new ReadModelBatchOptions { TimeProvider = clock };
var store = new DaprReadModelStore(client, Microsoft.Extensions.Options.Options.Create(options));
var raw = new DaprReadModelBatchStateAccessor(client, "statestore");
var scope = new SharedProjectionScope("statestore", "tenant", "widget", "family", ["writer"]);
var coordinator = new SharedProjectionEpochCoordinator(store, store, clock);
if (probe == "b1") {
 var batch = new ReadModelBatch(new("statestore", "tenant", "widget", "family", "family", "expiry"),
 [ReadModelBatchOperation.WriteExpiring("private",new JsonObject { ["count"]=1 },ReadModelBatchConcurrency.LastWrite,clock.GetUtcNow().AddSeconds(1)),
 ReadModelBatchOperation.Write("checkpoint",new JsonObject { ["count"]=1 },ReadModelBatchConcurrency.LastWrite)]);
 client.TryDeleteByteOverride = key => key=="private" ? false : null;
 var protocol = new ReadModelBatchProtocol(raw,options,NullLogger.Instance);
 var result = await protocol.ExecuteAsync(batch,new ProbeFault((phase,ordinal)=> { if (phase==ReadModelBatchPhase.BeforeCompaction) clock.Now=clock.Now.AddSeconds(2); }),default);
 Console.WriteLine($"result={result.Status}; own-envelope-remains={ReadModelBatchEnvelope.IsEnvelope(client.ByteStoreValue("private"))}; logical-visible={(await store.GetAsync<JsonObject>("statestore","private")).Value is not null}; raw-bytes={client.ByteStoreValue("private").Length}");
}
if (probe == "b3") {
 await coordinator.RegisterWriterAsync(scope,"writer");
 await coordinator.BeginAsync(scope,"op","inventory",new Dictionary<string,long>());
 var state = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName,scope.StateKey);
 var batch = new ReadModelBatch(new(scope.StoreName,scope.TenantId,scope.Domain,scope.Family,scope.Family,"retained-scratch"),
 [ReadModelBatchOperation.Write("private",new JsonObject { ["count"]=1 },ReadModelBatchConcurrency.CreateOnly),ReadModelBatchOperation.Write(scope.StateKey,state.Value!,ReadModelBatchConcurrency.Match(state.ETag!))]) { RetainManifestForRecovery=true };
 var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
 var accessor = new ReviewThreeBatchAccessor(raw);
 accessor.BeforeWrite=async(key,bytes,token)=>{if(key!="private"||!ReadModelBatchEnvelope.IsEnvelope(bytes.Span))return; accessor.BeforeWrite=null;paused.SetResult();await release.Task.WaitAsync(token);};
 var delayed = new ReadModelBatchProtocol(accessor,options,NullLogger.Instance).StageAsync(batch,null,default);
 await paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
 Console.WriteLine("helper-stage="+(await store.StageAsync(batch)).Status);
 Console.WriteLine("helper-abort="+(await new ReadModelBatchProtocol(raw,options,NullLogger.Instance).AbortStagedAsync(batch,null,default)).Status);
 await coordinator.ReclaimScratchReceiptAsync(scope,batch.Scope.BatchId);
 client.AfterTrySaveByteState=(key,bytes)=>{if(key=="private"&&ReadModelBatchEnvelope.IsEnvelope(bytes.Span))throw new InvalidOperationException("installer exited");};
 release.SetResult(); try { Console.WriteLine("delayed="+(await delayed).Status); } catch(Exception ex) { Console.WriteLine("delayed="+ex.Message); }
 client.AfterTrySaveByteState=null;
 var result = await store.RecoverPendingAsync(batch.Scope,"private");
 var fence=await ReadModelBatchTerminalFence.ReadAsync(raw,batch.Scope,ReadModelBatchFingerprint.Compute(batch),default);
 Console.WriteLine($"orphan={client.ByteStoreContains("private")}; retirement-anchors={fence?.Anchors.Length}; recovered={result.IsResolved}; reason={result.Reason}");
}
if (probe == "b4") {
 await coordinator.RegisterWriterAsync(scope,"writer"); await coordinator.BeginAsync(scope,"op","inventory",new Dictionary<string,long>());
 var state = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName,scope.StateKey);
 var batch = new ReadModelBatch(new(scope.StoreName,scope.TenantId,scope.Domain,scope.Family,scope.Family,"legacy"),[ReadModelBatchOperation.Write("private",new JsonObject { ["data"]=new string('a',600000) },ReadModelBatchConcurrency.CreateOnly),ReadModelBatchOperation.Write(scope.StateKey,state.Value!,ReadModelBatchConcurrency.Match(state.ETag!))]);
 Console.WriteLine("original-manifest-bytes="+ReadModelBatchFingerprint.BuildCanonicalManifest(batch).Length);
 Console.WriteLine("stage="+(await store.StageAsync(batch)).Status);
 var marker=ReadModelBatchMarker.ReadProof(client.ByteStoreValue(batch.Scope.ComputeMarkerKey()))!;
 var result=await ReadModelLegacyScratchRecovery.TryCompensateAsync(raw,options,batch.Scope,marker,"private",default);
 Console.WriteLine($"resolved={result?.IsResolved}; reason={result?.Reason}; private-remains={client.ByteStoreContains("private")}");
}
if (probe == "b6") {
 var fake=new InMemoryReadModelStore(); fake.BatchOptions.TimeProvider=clock;
 var batch=new ReadModelBatch(new("statestore","tenant","widget","family","family","expiry"),[ReadModelBatchOperation.WriteExpiring("private",new JsonObject { ["count"]=1 },ReadModelBatchConcurrency.LastWrite,clock.Now.AddSeconds(1))]);
 Console.WriteLine("fake-write="+(await fake.ExecuteAsync(batch)).Status); Console.WriteLine("dapr-write="+(await store.ExecuteAsync(batch)).Status); clock.Now=clock.Now.AddSeconds(1);
 Console.WriteLine($"fake-value={(await fake.GetAsync<JsonObject>("statestore","private")).Value is not null}; fake-etag-present={(await fake.TryReadEtagAsync("statestore","private")).Present}; dapr-value={(await store.GetAsync<JsonObject>("statestore","private")).Value is not null}; dapr-etag-present={(await store.TryReadEtagAsync("statestore","private")).Present}");
}

if (probe == "b5") {
 var fake=new InMemoryReadModelStore(); var future=new FoldersClock(); fake.BatchOptions.TimeProvider=future;
 var services=new Microsoft.Extensions.DependencyInjection.ServiceCollection();
 Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IReadModelStore>(services,fake);
 Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IReadModelBatchStore>(services,fake);
 Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IReadModelPendingBatchRecoveryStore>(services,fake);
 Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<Hexalith.Folders.Projections.TenantAccess.IUtcClock>(services,future);
 Hexalith.Folders.FoldersServiceCollectionExtensions.AddFoldersOperationalStatusPersistence(services);
 using var provider=Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
 var writer=Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Hexalith.Folders.Projections.OperationalStatus.OperationalStatusProjectionWriter>(provider);
 var inner=(SharedProjectionEpochCoordinator)writer.GetType().GetField("_coordinator",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(writer)!;
 var selectedClock=(TimeProvider)typeof(SharedProjectionEpochCoordinator).GetField("_timeProvider",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(inner)!;
 Console.WriteLine($"writer-configured-clock={future.GetUtcNow():O}; coordinator-clock={selectedClock.GetUtcNow():O}; shares-clock={ReferenceEquals(future,selectedClock)}");
 var cow=Hexalith.Folders.Projections.OperationalStatus.OperationalStatusGeneration.Scope("tenant-a");
 var proper=new SharedProjectionEpochCoordinator(fake,fake,future);
 await proper.RegisterWriterAsync(cow,cow.RequiredWriters[0]);
 var deadline=future.GetUtcNow().AddMinutes(1);
 await fake.ExecuteAsync(new ReadModelBatch(new(cow.StoreName,cow.TenantId,cow.Domain,"owner",cow.Family,"anchor"),[ReadModelBatchOperation.WriteExpiring(cow.PhysicalKey(0,"index"),new JsonObject{["count"]=1},ReadModelBatchConcurrency.CreateOnly,deadline)]));
 await proper.BeginAsync(cow,"abandoned","capture",new Dictionary<string,long>());
 await proper.StageStreamingAsync(cow,"abandoned",Replacement());
 await proper.AbortAsync(cow,"abandoned",(_,_,_)=>Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
 future.Now=deadline;
 Console.WriteLine($"proper-expired-read-available={(await proper.ReadAsync<JsonObject>(cow,"index")).IsAvailable}");
 try {var value=await inner.ReadAsync<JsonObject>(cow,"index");Console.WriteLine($"writer-expired-read-available={value.IsAvailable}");}catch(Exception ex){Console.WriteLine("writer-expired-read-error="+ex);}
 static async IAsyncEnumerable<ReadModelBatchOperation> Replacement(){yield return ReadModelBatchOperation.Write("index",new JsonObject{["count"]=99},ReadModelBatchConcurrency.LastWrite);await Task.Yield();}
}

if (probe == "b9") {
 using var otherClient=new RecordingDaprClient { PersistTypedState=true };
 var otherStore=new DaprReadModelStore(otherClient);
 var otherCoordinator=new SharedProjectionEpochCoordinator(otherStore,otherStore);
 var otherScope=new SharedProjectionScope("statestore","tenant","widget","maintenance",["writer"]);
 await otherCoordinator.RegisterWriterAsync(otherScope,"writer");await otherCoordinator.BeginAsync(otherScope,"first","inventory",new Dictionary<string,long>());
 await otherCoordinator.StageStreamingAsync(otherScope,"first",Rows());
 var failed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
 otherClient.AfterTrySaveByteState=(key,bytes)=>{
  if(key!=otherScope.StateKey||ReadModelBatchEnvelope.IsEnvelope(bytes.Span))return;
  var state=System.Text.Json.JsonSerializer.Deserialize<SharedProjectionEpochState>(bytes.Span,System.Text.Json.JsonSerializerOptions.Web);
  if(state is not { Phase:SharedProjectionEpochPhase.Open,CleanupStreamPageCount:>0 })return;
  otherClient.AfterTrySaveByteState=null;
  otherClient.BeforeReadByteState=key=>{otherClient.BeforeReadByteState=null;failed.SetResult();throw new IOException("maintenance-read-failure");};
 };
 await otherCoordinator.CommitAsync(otherScope,"first");await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));await otherCoordinator.WaitForPromotedCleanupAsync(otherScope);
 var stateAfter=await otherStore.GetAsync<SharedProjectionEpochState>(otherScope.StoreName,otherScope.StateKey);
 var statusAfter=await otherCoordinator.GetStatusAsync(otherScope);
 Console.WriteLine($"cleanup-wait-returned-success=True; remaining-cleanup-pages={stateAfter.Value!.CleanupStreamPageCount}; status-degraded={statusAfter.IsDegraded}; status-building={statusAfter.IsBuilding}");
 static async IAsyncEnumerable<ReadModelBatchOperation> Rows(){yield return ReadModelBatchOperation.Write("row",new JsonObject{["count"]=1},ReadModelBatchConcurrency.LastWrite);await Task.Yield();}
}
sealed class ProbeClock:TimeProvider { public DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:00:00Z"); public override DateTimeOffset GetUtcNow()=>Now; }
sealed class ProbeFault(Action<ReadModelBatchPhase,int> action):IReadModelBatchFaultInjector { public Task InjectAsync(ReadModelBatchPhase phase,int operationIndex,CancellationToken token) { action(phase,operationIndex);return Task.CompletedTask; } }

sealed class FoldersClock:TimeProvider,Hexalith.Folders.Projections.TenantAccess.IUtcClock { public DateTimeOffset Now=DateTimeOffset.Parse("2040-01-01T00:00:00Z"); public DateTimeOffset UtcNow=>Now; public override DateTimeOffset GetUtcNow()=>Now; }
