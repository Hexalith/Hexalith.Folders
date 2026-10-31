# Additive inventory reference format

This continuation changes snapshot serialization and traversal only. Events,
mandatory probes, loss counters, deadlines, readiness fields and output limits
retain their existing contract. Snapshots remain racy observations.

Every task has its own task row. Every resource reference carries the target
PID/TID, group-leader start ticks and task start ticks. Proc ticks remain rounded;
event identities retain exact kernel group-leader start boottime.

`shared_mapping_inventory` ends a full per-task mapping observation and records
its interval and completeness, including an empty inventory. A later
`shared_mapping_reference` points to that source task and interval only after
KCMP_VM equality and both tasks' start identities are checked before and after
the target observation. `descriptor_table_reference` uses the same identity and
interval fields with KCMP_FILES proof. References retain the original source
observation interval; they do not claim contemporaneous or atomic resource
contents. Unknown/private resources enumerate fully. A failed recheck stays an
error and does not emit a confirmed reference. Sources with errors are not
selected for resource references.

Each full `descriptor` row has an `observation_id`, scoped to that process
snapshot, and an observation timestamp. `descriptor_reference` retains the new
task/FD identity, timestamp and observation ID, pointing directly to a previous
full descriptor row by its complete scope/FD/observation ID/timestamp. Expand
only its `path`, `metadata` and `fd_metadata` fields from that source row. The
payload has been re-read using the ordinary race checks and compared byte for
byte as canonical JSON. Never copy the source task identity or source timestamp
over the new observation. The per-process payload cache is bounded at 4,096
entries and 4 MiB of serialized keys; saturation emits full rows. Process and
snapshot boundaries reset it, and references never chain.

All snapshot errors still appear in JSONL and in phase counts. A traversal that
reaches its end can have `completed: true` and nonzero errors; that does not
satisfy `before_snapshot_complete` or the documented readiness handshake.
`ready.json` and receipt `ready` alone are insufficient. No error is subtracted,
ignored on retry or converted into a clean result.
