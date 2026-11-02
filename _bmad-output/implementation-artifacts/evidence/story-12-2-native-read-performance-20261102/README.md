# Story 12.2 native authority read continuation — 2026-11-02

## Scope and source

The user approved bounded performance work on the Tenants/EventStore native administrator source read, then explicitly answered “Yes, optimize bootstrap too” to a request for bounded bootstrap-path optimization after the first live attempts. The five-second domain-service deadline, Dapr's five-second attempt, every source read, native proof, final observation, route-catalog guard, and strict acceptance assertion remain in force. This packet records attempted qualification, not Story 12.2 completion.

Baseline HEADs recorded during this continuation before the concurrent branch and commit moves:

| Repository | HEAD |
| --- | --- |
| Folders | `17c664502e984083b0e779e679b3bca5bedfa0d7` |
| EventStore | `f2be45ede245211f549978358872875c5df28dc8` |
| Tenants | `ba4639fa462341281f89aa7a08b7398477fc1ba9` |

During the fourth live attempt, another process committed the EventStore and Tenants edits without this agent staging or committing them. Their clean HEADs are `6e498d1efcab4d82917eff25433dbf4efc291803` and `0416f4329a27847485cc9cbe7ec27c580b528c7d`. The Folders HEAD had advanced concurrently to `4953b16682a30df7d1acce70d1cb176e7d6095c1` before the fourth attempt. The same process later committed the Folders Story 12.2 AppHost/spec/evidence edits as `d7d9c8c86a87d8cab759334ffdf38ae62d026d26`; Folders subsequently advanced to `24351c1e` for unrelated Story 1.17 evidence. The executable hashes below bind the live runs independently of the Git HEAD moves.

The EventStore SDK now acquires one durable catalog lease for the complete native administrator read and rechecks independently observed local host facts on each scoped HTTP request. Lease reuse is restricted to the configured EventStore app ID and exact `POST api/v1/streams/read/workload` route. Existing endpoint, catalog inventory, binding header, and downstream invocation checks still run on every request. Tenants encloses the existing head/page/head/proof/final-observation sequence in that SDK scope. The AppHost exposes the EventStore sidecar's supported HTTP-port setting to isolate a disposable acceptance run from an already occupied default listener; the default remains 3501. A subsequent EventStore observation change computes each declaration digest once per fresh local observation instead of repeating it inside candidate comparisons; it preserves the same exact inventory checks. EventStore's native stream route also groups the existing ordered actor proof inspections and bounded range inside one actor turn. The client still performs head/page/head and final metadata observation, the actor still checks proof before and inside a page, and the controller still checks proof after readability work. A null batch uses the prior complete actor sequence; a malformed present batch fails closed.

## Verification

The affected projects built from project references in Debug with `UseNuGetDeps=false`, `HexalithEventStoreVersion=3.132.2`, `-m:1`, and `-nr:false`. The first AppHost build with 3.130.2 was rejected by the current authoritative catalog selection; rebuilding with 3.132.2 succeeded. The final EventStore controller/prefix/workload/metadata/native-scope group passed 111/111, zero errors or skips. The four affected SDK catalog/native-scope classes passed after the fresh-observation digest change. Tenants Server tests passed 1,797/1,797, and its native gateway integration class passed 25/25. The AppHost port-model theory passed both default and selected-port cases. A broader 692-case SDK routing run was interrupted after 157.383 seconds: 691 cases had completed and one unrelated host test failed by cancellation from the stop signal. It is not a passing broad suite. These are focused results; no broad regression claim follows from them.

Final affected source build and focused actor/HTTP verification commands, run from the owning repository roots:

```bash
# From references/Hexalith.EventStore
dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -c Debug --no-restore -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreVersion=3.132.2 -m:1 -nr:false -v:quiet
dotnet tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Controllers.StreamsControllerTests -class Hexalith.EventStore.Server.Tests.Streams.NativeSourcePrefixTests -class Hexalith.EventStore.Server.Tests.Controllers.WorkloadGatewayControllerTests -class Hexalith.EventStore.Server.Tests.Streams.NativeSourceMetadataReaderTests -class Hexalith.EventStore.Server.Tests.Routing.NativeSourceReadScopeTests

# From Folders
dotnet build tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj -c Debug --no-restore -p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false -p:HexalithEventStoreVersion=3.132.2 -m:1 -nr:false -v:quiet
```

The strict real-provider command was the unchanged class invocation below, first with the default port and then with `Folders__EventStoreDaprHttpPort=35991`, `35992`, and `35993`:

```bash
HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true \
  dotnet tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll \
  -class Hexalith.Folders.AppHost.Tests.FolderDurabilityAcceptanceTests -showLiveOutput

Folders__EventStoreDaprHttpPort=35991 HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true \
  dotnet tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll \
  -class Hexalith.Folders.AppHost.Tests.FolderDurabilityAcceptanceTests -showLiveOutput

Folders__EventStoreDaprHttpPort=35992 HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true \
  dotnet tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll \
  -class Hexalith.Folders.AppHost.Tests.FolderDurabilityAcceptanceTests -showLiveOutput

Folders__EventStoreDaprHttpPort=35993 HEXALITH_FOLDERS_RUN_ASPIRE_INTEGRATION=true \
  dotnet tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net10.0/Hexalith.Folders.AppHost.Tests.dll \
  -class Hexalith.Folders.AppHost.Tests.FolderDurabilityAcceptanceTests -showLiveOutput
```

| Attempt | Result | First blocking observation | Private raw log SHA-256 |
| --- | --- | --- | --- |
| Default 3501 | 1 failed, 0 errors/skips; 150.887 s | Route-catalog readback timed out during bootstrap; another local AppHost held port 3501. | `3ad63acab12bb6e7661c0e1932a96ead4da03b944494766bcc9b426250e8c07c` |
| Isolated 35991 | 1 failed, 0 errors/skips; 86.379 s | Bootstrap returned HTTP 500 `ERR_DIRECT_INVOKE`. The retained `BootstrapGlobalAdmin` actor completed in 5,478.4952 ms, beyond Dapr's unchanged five-second attempt; retries did not recover. | `52fc287192bc1405249dbaceb6b01d65b47fe5924b1194748fd21a746e5c3ecf` |
| Isolated 35992, before actor grouping | 1 failed, 0 errors/skips; 53.035 s | Real bootstrap passed its 202/terminal/source/projection checks. `CreateTenant` received HTTP 500 `unavailable`: native head returned 200 in 869 ms, page in 2,446 ms, and the next head was pending when the unchanged five-second domain-service invocation expired. | `137a66ef3e3d9dc3811fa314e1b57f2f4d3e56408c9ae8e4b57f91416deb2da0` |
| Isolated 35993, grouped actor reads | 1 failed, 0 errors/skips; 123.916 s | Real bootstrap passed. `CreateTenant` completed in 4,396.9103 ms and the strict body passed tenant-authority. Its first `InitializeOrganizationAclBaseline` HTTP request then exceeded the unchanged 40-second test-client timeout. The actor completed the command; the gateway later spent 16,801 ms on projection delivery plus repeated execution-fence awaits before the client timed out. | `f2891e1fea9be7e1cf420374adb00de6492e7e87a1960c75be509c1238fa7b69` |

The raw logs are mode 0600 at `/tmp/story12-native-read-strict-20261102.log`, `/tmp/story12-native-read-strict-port35991-20261102.log`, `/tmp/story12-native-read-strict-port35992-20261102.log`, and `/tmp/story12-native-read-strict-port35993-20261102.log`. They are not copied into this packet because live output can contain credentials. The first two attempts preceded the final SDK source. SDK ServiceDefaults DLLs in the EventStore source build, Tenants host, Folders Server, AppHost and test runner were byte-identical for the later attempts: SHA-256 `295fe8ee4d079792ea87eb91bcca32f5b3490c4b6bfb1371bc2e5a071f880ddb`. For the fourth, EventStore Server DLLs in its source build and launched host were byte-identical: SHA-256 `6d6ccf12944aff55144aa1a7d6ce23592d2b22e2194636f7abcadc2b55b872db`.

The third attempt passed the listener collision and fully verified owner bootstrap, then reached native administrator reads during `CreateTenant`. The fourth completed that native read and entered the strict body, but did not reach its six durability concerns or any SM4 measurement. For the third run, the owned Dapr run directory `/tmp/hexalith-dapr/2c702ce5065f40199bfc3f4964fe8143` was absent after teardown, port 35992 was free, and no container matching the disposable run remained. For the fourth, `/tmp/hexalith-dapr/f957126960b246a38fc727404073249a` was absent, port 35993 was free, and the disposable container count was zero. Earlier attempts also left no observed owned AppHost process, container or temporary directory; no unrelated running AppHost was stopped.

## Disposition

The strict live task and whole story remain in progress. The bounded bootstrap observation optimization was exercised by the third and fourth runs, and genuine bootstrap passed. The grouped native read completed within the unchanged domain-service deadline in the fourth run. The current blocker is the broader EventStore command/projection-delivery pipeline's 40-second HTTP response on the first organization baseline command; that path is outside the two approved performance scopes. Broad SDK, Release/package/PD10, SM4, and independent whole-story review gates remain open.
