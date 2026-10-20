using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService.Tests;
try { await new DomainPartitionedSharedProjectionRebuildTests().InterruptedAccumulateRetryOrDirectAbortPreservesSelectionAndPermitsFreshRebuild(ReadModelBatchPhase.BeforeInstallOperation,0,false,false); Console.WriteLine("PASSED"); } catch(Exception e) { Console.WriteLine(e); }
