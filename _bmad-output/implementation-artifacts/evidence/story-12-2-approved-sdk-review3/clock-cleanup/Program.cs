using System.Reflection;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
var clock = new FixedClock();
var services = new ServiceCollection();
services.AddSingleton(Substitute.For<DaprClient>());
services.AddEventStoreReadModelStore(options => options.TimeProvider = clock);
using var provider = services.BuildServiceProvider();
var registered = provider.GetRequiredService<SharedProjectionEpochCoordinator>();
var actualClock = typeof(SharedProjectionEpochCoordinator).GetField("_timeProvider", BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(registered);
Console.WriteLine("registered store clock same configured: " + ReferenceEquals(provider.GetRequiredService<IOptions<ReadModelBatchOptions>>().Value.TimeProvider, clock));
Console.WriteLine("registered coordinator clock same configured: " + ReferenceEquals(actualClock, clock));
var store = new InMemoryReadModelStore();
var scope = new SharedProjectionScope("statestore", "tenant", "widget", "index", ["writer"]);
var coordinator = new SharedProjectionEpochCoordinator(store, store);
await coordinator.RegisterWriterAsync(scope, "writer");
await coordinator.BeginAsync(scope,"first","inventory",new Dictionary<string,long>());
await coordinator.StageStreamingAsync(scope,"first",Rows());
var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
int stops=0;bool cleanupTokenCanCancel=true;
store.BatchFaultHook=async(phase,_,token)=>{if(phase==ReadModelBatchPhase.BeforeReceipt && Interlocked.Increment(ref stops)==1){cleanupTokenCanCancel=token.CanBeCanceled;paused.TrySetResult();await release.Task.WaitAsync(token);}};
await coordinator.CommitAsync(scope,"first");await paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
try{
 for(int i=0;i<2;i++){
  using var cancel=new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
  try{await coordinator.BeginAsync(scope,"next","next-inventory",new Dictionary<string,long>(),cancel.Token);Console.WriteLine("Begin unexpectedly completed");}
  catch(OperationCanceledException){Console.WriteLine("Begin caller canceled: "+i);}
 }
 Console.WriteLine("background cleanup token can cancel: "+cleanupTokenCanCancel);
 Console.WriteLine("background cleanup remains pending: "+!coordinator.WaitForPromotedCleanupAsync(scope).IsCompleted);
}finally{release.TrySetResult();await coordinator.WaitForPromotedCleanupAsync(scope).WaitAsync(TimeSpan.FromSeconds(10));}
static async IAsyncEnumerable<ReadModelBatchOperation> Rows(){yield return ReadModelBatchOperation.Write("row",new JsonObject{["count"]=1},ReadModelBatchConcurrency.LastWrite);await Task.Yield();}
sealed class FixedClock:TimeProvider{public override DateTimeOffset GetUtcNow()=>DateTimeOffset.UtcNow.AddYears(1);}
