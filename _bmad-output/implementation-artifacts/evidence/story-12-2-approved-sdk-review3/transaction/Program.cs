using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Testing.Fakes;
var store=new InMemoryReadModelStore();store.BatchOptions.StoreProfiles["statestore"]=ReadModelBatchStoreProfile.TransactionQualified;
var scope=new SharedProjectionScope("statestore","tenant","widget","index",["writer"]);
var c=new SharedProjectionEpochCoordinator(store,store);
await c.RegisterWriterAsync(scope,"writer");await c.BeginAsync(scope,"operation","inventory",new Dictionary<string,long>());
store.SeedRaw("statestore","private",new JsonObject{["count"]=9});
Console.WriteLine("scratch: "+await c.TryWriteScratchAsync(scope,"operation","private",new JsonObject{["count"]=1},"scratch",ReadModelBatchConcurrency.CreateOnly));
var bs=new ReadModelBatchScope("statestore","tenant","widget","index","index","scratch");
Console.WriteLine("recovery: "+await store.RecoverPendingAsync(bs,bs.ComputeMarkerKey()));
Console.WriteLine("marker: "+store.Snapshot<ReadModelBatchMarker>("statestore",bs.ComputeMarkerKey())!.Status);
try{await c.ReclaimScratchReceiptAsync(scope,"scratch");Console.WriteLine("reclaim success");}catch(Exception ex){Console.WriteLine("reclaim failure: "+ex);}
