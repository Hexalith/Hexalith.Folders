using System.Text.Json.Nodes;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
var measurements=new System.Collections.Concurrent.ConcurrentBag<string>();
using var listener=new MeterListener();listener.InstrumentPublished=(instrument,l)=>{if(instrument.Name=="eventstore.projection.cleanup.failures")l.EnableMeasurementEvents(instrument);};listener.SetMeasurementEventCallback<long>((_,value,tags,_)=>{foreach(var tag in tags)if(tag.Key=="reason")measurements.Add(tag.Value?.ToString()??"null");});listener.Start();var elapsed=Stopwatch.StartNew();

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
  otherClient.BeforeReadByteState=key=>{otherClient.BeforeReadByteState=null;failed.SetResult();throw new OperationCanceledException("dependency-cancelled-before-budget");};
 };
 await otherCoordinator.CommitAsync(otherScope,"first");await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));await otherCoordinator.WaitForPromotedCleanupAsync(otherScope);
 var stateAfter=await otherStore.GetAsync<SharedProjectionEpochState>(otherScope.StoreName,otherScope.StateKey);
 var statusAfter=await otherCoordinator.GetStatusAsync(otherScope);
 Console.WriteLine($"cleanup-wait-returned-success=True; remaining-cleanup-pages={stateAfter.Value!.CleanupStreamPageCount}; status-degraded={statusAfter.IsDegraded}; status-building={statusAfter.IsBuilding}");
 static async IAsyncEnumerable<ReadModelBatchOperation> Rows(){yield return ReadModelBatchOperation.Write("row",new JsonObject{["count"]=1},ReadModelBatchConcurrency.LastWrite);await Task.Yield();}
Console.WriteLine("Reason="+string.Join(",",measurements)+"; elapsed_ms="+elapsed.ElapsedMilliseconds+"; configured_budget_ms=30000");
