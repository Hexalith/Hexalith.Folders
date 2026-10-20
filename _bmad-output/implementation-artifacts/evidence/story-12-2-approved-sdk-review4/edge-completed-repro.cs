using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Tests.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using var client = new RecordingDaprClient { PersistTypedState = true };
var store = new DaprReadModelStore(client);
var scope = new SharedProjectionScope("statestore", "tenant", "widget", "family", ["writer"]);
var coordinator = new SharedProjectionEpochCoordinator(store, store);
await coordinator.RegisterWriterAsync(scope,"writer");
await coordinator.BeginAsync(scope,"op","inventory",new Dictionary<string,long>());
var state = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName,scope.StateKey);
var batch = new ReadModelBatch(new(scope.StoreName,scope.TenantId,scope.Domain,scope.Family,scope.Family,"retained-scratch"),
[ReadModelBatchOperation.Write("private",new JsonObject{["count"]=1},ReadModelBatchConcurrency.CreateOnly),
ReadModelBatchOperation.Write(scope.StateKey,state.Value!,ReadModelBatchConcurrency.Match(state.ETag!))]) { RetainManifestForRecovery=true };
var paused=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var accessor=new ReviewThreeBatchAccessor(new DaprReadModelBatchStateAccessor(client,scope.StoreName));
accessor.BeforeWrite=async(key,bytes,token)=>{
 if(key!="private" || !ReadModelBatchEnvelope.IsEnvelope(bytes.Span))return;
 accessor.BeforeWrite=null; paused.SetResult(); await release.Task.WaitAsync(token);
};
Task<ReadModelBatchResult> delayed=new ReadModelBatchProtocol(accessor,new ReadModelBatchOptions(),NullLogger.Instance).ExecuteAsync(batch,null,default);
await paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
Console.WriteLine("Helper => "+(await store.ExecuteAsync(batch)).Status);
var scratch=await store.GetAsync<JsonObject>(scope.StoreName,"private");
Console.WriteLine("Erase scratch => "+await store.TryEraseAsync(scope.StoreName,"private",scratch.ETag!));
await coordinator.ReclaimScratchReceiptAsync(scope,batch.Scope.BatchId);
Console.WriteLine("Marker after cleanup => "+client.ByteStoreContains(batch.Scope.ComputeMarkerKey()));
await coordinator.AbortAsync(scope,"op",(_,_,_)=>Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
client.AfterTrySaveByteState=(key,bytes)=>{if(key=="private"&&ReadModelBatchEnvelope.IsEnvelope(bytes.Span))throw new InvalidOperationException("installer exited");};
release.SetResult();
try{Console.WriteLine("Delayed => "+(await delayed).Status);}catch(Exception ex){Console.WriteLine("Delayed => "+ex.Message);}
client.AfterTrySaveByteState=null;
Console.WriteLine("Private raw => "+System.Text.Encoding.UTF8.GetString(client.ByteStoreValue("private")));
var result=await store.RecoverPendingAsync(batch.Scope,"private");
Console.WriteLine("Recovery => "+result.IsResolved+" / "+result.Reason);
