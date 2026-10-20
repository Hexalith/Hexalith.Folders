using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
var store = new InMemoryReadModelStore();
var scope = new SharedProjectionScope("statestore", "tenant", "widget", "index", ["writer"]);
var coordinator = new SharedProjectionEpochCoordinator(store, store);
await coordinator.RegisterWriterAsync(scope, "writer");
await coordinator.BeginAsync(scope,"legacy","inventory",new Dictionary<string,long>());
var state=await store.GetAsync<SharedProjectionEpochState>(scope.StoreName,scope.StateKey);
var batch=new ReadModelBatch(new(scope.StoreName,scope.TenantId,scope.Domain,scope.Family,scope.Family,"legacy-scratch"),[ReadModelBatchOperation.Write("private",new JsonObject{["count"]=1},ReadModelBatchConcurrency.CreateOnly),ReadModelBatchOperation.Write(scope.StateKey,state.Value!,ReadModelBatchConcurrency.Match(state.ETag!))]);
store.BatchFaultHook=(phase,_,_)=>phase==ReadModelBatchPhase.BeforeCommit?throw new InvalidOperationException("stop"):Task.CompletedTask;
try { await store.ExecuteAsync(batch); } catch(Exception ex) { Console.WriteLine(ex.Message); }
store.BatchFaultHook=null;
for(var i=0;i<4;i++)Console.WriteLine("retry "+i+" => "+await coordinator.TryWriteScratchAsync(scope,"legacy","private",new JsonObject{["count"]=1},"legacy-scratch",ReadModelBatchConcurrency.CreateOnly));
Console.WriteLine("scratch = "+(await store.GetAsync<JsonObject>(scope.StoreName,"private")).Value);
var cleanupStore = new InMemoryReadModelStore();
cleanupStore.SeedRaw("statestore","cleanup-private",new JsonObject{["count"]=1});
var cleanupIdentity=new Hexalith.EventStore.DomainService.DomainSharedProjectionRebuildIdentity("tenant","widget","index","op","catalog");
var cleanupMethod=typeof(Hexalith.EventStore.DomainService.DomainSharedProjectionRebuildDispatcher).GetMethod("DeleteScratchKeysAsync",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
Task DeleteCleanup()=> (Task)cleanupMethod.Invoke(null,[cleanupStore,cleanupStore,cleanupIdentity,"statestore","chunks:0:0",new[]{"cleanup-private"},CancellationToken.None,scope])!;
cleanupStore.BatchFaultHook=(phase,_,_)=>phase==ReadModelBatchPhase.BeforeCommit?throw new InvalidOperationException("cleanup stop"):Task.CompletedTask;
try{ await DeleteCleanup(); }catch(Exception ex){Console.WriteLine("first cleanup => "+ex.Message);}
cleanupStore.BatchFaultHook=null;
try{ await DeleteCleanup(); }catch(Exception ex){Console.WriteLine("retry cleanup => "+ex.Message);}
var holeStore=new InMemoryReadModelStore();
holeStore.SeedRaw("statestore","occupied",new JsonObject{["count"]=2});
var holeBatch=new ReadModelBatch(new("statestore","tenant","widget","owner","projection","expired-prefix"),[ReadModelBatchOperation.WriteExpiring("expired",new JsonObject{["count"]=1},ReadModelBatchConcurrency.LastWrite,DateTimeOffset.MinValue),ReadModelBatchOperation.Write("occupied",new JsonObject{["count"]=1},ReadModelBatchConcurrency.CreateOnly)]);
var holeResult=await holeStore.ExecuteAsync(holeBatch); Console.WriteLine("expired prefix conflict => "+holeResult.Status+" / "+holeResult.RecoveryReason);
var badFenceStore=new InMemoryReadModelStore(); var badCoordinator=new SharedProjectionEpochCoordinator(badFenceStore,badFenceStore);
await badCoordinator.RegisterWriterAsync(scope,"writer");await badCoordinator.BeginAsync(scope,"bad","inventory",new Dictionary<string,long>());
var badState=await badFenceStore.GetAsync<SharedProjectionEpochState>("statestore",scope.StateKey);
badFenceStore.SeedRaw("statestore",scope.StateKey,badState.Value! with{ActiveGeneration=-1});
badState=await badFenceStore.GetAsync<SharedProjectionEpochState>("statestore",scope.StateKey);
var badBatch=new ReadModelBatch(new(scope.StoreName,scope.TenantId,scope.Domain,scope.Family,scope.Family,"bad-legacy-scratch"),[ReadModelBatchOperation.Write("bad-private",new JsonObject{["count"]=1},ReadModelBatchConcurrency.CreateOnly),ReadModelBatchOperation.Write(scope.StateKey,badState.Value!,ReadModelBatchConcurrency.Match(badState.ETag!))]);
badFenceStore.BatchFaultHook=(phase,_,_)=>phase==ReadModelBatchPhase.BeforeCommit?throw new InvalidOperationException("stop"):Task.CompletedTask;
try{await badFenceStore.ExecuteAsync(badBatch);}catch{} badFenceStore.BatchFaultHook=null;
var badRecovered=await badFenceStore.RecoverPendingAsync(badBatch.Scope,"bad-private");Console.WriteLine("invalid legacy fence recovery => "+badRecovered.IsResolved+"; private raw exists => "+(badFenceStore.SnapshotRaw("statestore","bad-private") is not null));
