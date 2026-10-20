using System.Text.Json.Nodes;
using System.Text;
using Hexalith.EventStore.Client.Projections;
using Microsoft.Extensions.Logging.Abstractions;

var a=new Accessor();
string longText=new string('<',512);
var sc=new ReadModelBatchScope(longText,longText,longText,longText,longText,longText);
sc.Validate();
string fp="v1:"+new string('A',43);
var anchors=Enumerable.Range(0,2).Select(i=>new ReadModelBatchRecoveryAnchor(sc,"key"+i,i,false,ReadModelBatchRecoveryAnchor.Digest("{}"u8.ToArray()),null,null)).ToArray();
var fence=new ReadModelBatchTerminalFence(sc,fp,anchors);
Console.WriteLine($"Legacy fence bytes={ReadModelBatchCanonicalJson.Serialize(fence).Length}, seal={await fence.SealAsync(a,default)}");
var scope=new ReadModelBatchScope("store","tenant","domain","family","family","batch");
var anchor=new ReadModelBatchRecoveryAnchor(scope,"model",0,false,ReadModelBatchRecoveryAnchor.Digest("{}"u8.ToArray()),null,null);
var bad=new ReadModelBatchParticipantProof(scope,fp,anchor,(ReadModelBatchConcurrencyMode)999,"malformed");
a.Rows[ReadModelBatchParticipantProof.Key(scope,fp,0)]=ReadModelBatchCanonicalJson.Serialize(bad);
Console.WriteLine($"Malformed proof anchor accepted={await ReadModelBatchParticipantProof.MatchesAnchorAsync(a,scope,fp,anchor,default)}");

// A terminal receipt's missing anchor does not bind logical-key discovery to the caller's boundary.
var foreign=scope with{TenantId="other-tenant",BatchId="discovery"};
var env=new ReadModelBatchEnvelope {ScopeHash=scope.ComputeScopeHash(),Fingerprint=fp,Ordinal=0,IsDelete=false,CandidateBase64=Convert.ToBase64String("{}"u8),PreviousBase64=null};
a.Rows["model"]=env.ToBytes();
a.Rows[scope.ComputeMarkerKey()]=ReadModelBatchCanonicalJson.Serialize(new ReadModelBatchMarker {ScopeHash=scope.ComputeScopeHash(), BatchId=scope.BatchId,Fingerprint=fp,Status=ReadModelBatchMarkerStatus.Completed,TerminalTimeUtc=DateTimeOffset.UtcNow.ToString("O"),RetainedParticipantCount=1});
int before=a.Writes.Count;
var res=await ReadModelPendingBatchRecovery.RecoverAsync(a,new ReadModelBatchOptions(),NullLogger.Instance,foreign,"model",null,default);
Console.WriteLine($"Foreign recovery result={res.IsResolved}/{res.Reason}; writes={a.Writes.Count-before}");
foreach(var key in a.Writes.Skip(before)) Console.WriteLine("Foreign recovery wrote "+key);

// A foreign pending envelope is a deterministic conditional conflict, but the tx preflight leaves Prepared forever.
var txStore=new Hexalith.EventStore.Testing.Fakes.InMemoryReadModelStore();
var foreignBatch=new ReadModelBatch(new("store","tenant","domain","owner","view","foreign"),[ReadModelBatchOperation.Write("busy",new JsonObject { ["v"]=1 },ReadModelBatchConcurrency.CreateOnly)]);
Console.WriteLine("Foreign stage="+(await txStore.StageAsync(foreignBatch)).Status);
txStore.BatchOptions.StoreProfiles["store"]=ReadModelBatchStoreProfile.TransactionQualified;
var txBatch=new ReadModelBatch(foreignBatch.Scope with {BatchId="tx"},[ReadModelBatchOperation.WriteExpiring("busy",new JsonObject { ["v"]=2 },ReadModelBatchConcurrency.CreateOnly,DateTimeOffset.UtcNow.AddDays(1))]);
for(int i=0;i<2;i++){var r=await txStore.ExecuteAsync(txBatch);Console.WriteLine($"Tx attempt {i}={r.Status}/{r.RecoveryReason}");}
Console.WriteLine("Tx marker="+Encoding.UTF8.GetString(txStore.SnapshotRaw("store",txBatch.Scope.ComputeMarkerKey())!));

// Terminal legacy recovery bypasses the canonical compensation-key check used by Prepared recovery.
var legacyScope=scope with {BatchId="legacy"};
var legacyEnvs=Enumerable.Range(0,2).Select(i=>new ReadModelBatchEnvelope {ScopeHash=legacyScope.ComputeScopeHash(), Fingerprint=fp, Ordinal=i,IsDelete=false,CandidateBase64=Convert.ToBase64String("{}"u8),PreviousBase64=null}).ToArray();
var legacyAnchors=legacyEnvs.Select((e,i)=>new ReadModelBatchRecoveryAnchor(legacyScope,"legacy-key"+i,i,false,ReadModelBatchRecoveryAnchor.Digest(e.CandidateBytes()),null,null)).ToArray();
await new ReadModelBatchTerminalFence(legacyScope,fp,legacyAnchors).SealAsync(a,default);
for(int i=0;i<2;i++)a.Rows["legacy-key"+i]=legacyEnvs[i].ToBytes();
a.Rows[legacyScope.ComputeMarkerKey()]=ReadModelBatchCanonicalJson.Serialize(new ReadModelBatchMarker {ScopeHash=legacyScope.ComputeScopeHash(),BatchId=legacyScope.BatchId,Fingerprint=fp,Status=ReadModelBatchMarkerStatus.Aborted,ScratchCompensationKey="unrelated-key", Operations=Enumerable.Range(0,2).Select(i=>new ReadModelBatchMarkerOperation {Key="legacy-key"+i,Ordinal=i,IsDelete=false}).ToList()});
var legacyRes=await ReadModelPendingBatchRecovery.RecoverAsync(a,new ReadModelBatchOptions(),NullLogger.Instance,legacyScope,legacyScope.ComputeMarkerKey(),null,default);
Console.WriteLine($"Noncanonical legacy compensation key accepted={legacyRes.IsResolved}/{legacyRes.Reason}; private row remains={a.Rows.ContainsKey("legacy-key0")}");

// The public consumer clock argument no longer suffices with default SDK store options.
var domainClock=new DomainClock();
var consumerStore=new Hexalith.EventStore.Testing.Fakes.InMemoryReadModelStore();
var writer=new Hexalith.Folders.Projections.OperationalStatus.OperationalStatusProjectionWriter(consumerStore,consumerStore,consumerStore,domainClock);
var created=new Hexalith.Folders.Aggregates.Folder.FolderCreated("tenant","tenant","folder","Folder",null,null,[],Hexalith.Folders.Aggregates.Folder.FolderLifecycleState.Active,Hexalith.Folders.Aggregates.Folder.FolderRepositoryBindingState.Unbound,"principal","corr","task","key","fp",domainClock.UtcNow);
var source=new Hexalith.EventStore.Contracts.Projections.ProjectionEventDto(created.GetType().FullName!,System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(created),"json",1,domainClock.UtcNow,"corr","msg"){MetadataVersion=1,GlobalPosition=1};
var evt=Hexalith.Folders.Projections.OperationalStatus.OperationalStatusEventReader.ReadStored(source);
var plan=await writer.PrepareRebuildAsync("tenant","folder",[evt],1);
var expiring=plan.First(op=>op.ExpiresAtUtc is not null);
var consumerResult=await consumerStore.ExecuteAsync(new ReadModelBatch(scope with{BatchId="consumer-clock"},plan));
Console.WriteLine($"Manual consumer clock={domainClock.UtcNow:O}; result={consumerResult.Status}; fresh emitted model present={(await consumerStore.GetAsync<JsonObject>("store",expiring.Key)).Value is not null}");

sealed class Accessor:IReadModelBatchStateAccessor {
public Dictionary<string,ReadOnlyMemory<byte>> Rows=new();public List<string> Writes=new();
public bool SupportsTransaction=>false;
public Task<RawStateEntry> ReadAsync(string key,CancellationToken ct)=>Task.FromResult(Rows.TryGetValue(key,out var b)?new RawStateEntry(true,b,"etag"):new RawStateEntry(false,default,""));
public Task<bool> TryWriteAsync(string key,ReadOnlyMemory<byte> b,string etag,CancellationToken ct)=>TryWriteAsync(key,b,etag,null,ct);
public Task<bool> TryWriteAsync(string key,ReadOnlyMemory<byte> b,string etag,TimeSpan? ttl,CancellationToken ct){if(etag.Length==0&&Rows.ContainsKey(key))return Task.FromResult(false);Rows[key]=b.ToArray();Writes.Add(key);return Task.FromResult(true);}
public Task<bool> TryDeleteAsync(string key,string etag,CancellationToken ct){Rows.Remove(key);Writes.Add(key);return Task.FromResult(true);}
public Task ExecuteTransactionAsync(IReadOnlyList<RawTransactionOperation> ops,CancellationToken ct)=>throw new NotImplementedException();
}

sealed class DomainClock:Hexalith.Folders.Projections.TenantAccess.IUtcClock {public DateTimeOffset UtcNow=>new(2020,1,1,0,0,0,TimeSpan.Zero);}
