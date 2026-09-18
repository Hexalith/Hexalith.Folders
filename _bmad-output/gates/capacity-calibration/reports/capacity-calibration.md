> test info



test suite: `nbomber_default_test_suite_name`

test name: `nbomber_default_test_name`

session id: `2026-09-18_17-25-23_9ffbc9e1`

> scenario stats



scenario: `folder_workspace_full_lifecycle`

  - duration: `00:00:09`

load simulations:

  - `inject`, rate: `2`, interval: `00:00:01`, during: `00:00:09`

|scenario and steps|ok stats|
|---|---|
|scenario name|`folder_workspace_full_lifecycle`|
|requests|total = `90`, ok = `90`, fail = `0`|
|RPS (req/sec)|total = `10`/s, ok = `10`/s, fail = `0`/s|
|latency (ms)|min = `0.02`, mean = `0.46`, max = `13.32`, StdDev = `1.7`|
|latency percentile (ms)|p50 = `0.08`, p75 = `0.11`, p95 = `2.49`, p99 = `5.8`|
|||
|step name|`prepare_workspace`|
|requests|total = `18`, ok = `18`, fail = `0`|
|RPS (req/sec)|total = `2`/s, ok = `2`/s, fail = `0`/s|
|latency (ms)|min = `0.08`, mean = `0.87`, max = `13.32`, StdDev = `3.02`|
|latency percentile (ms)|p50 = `0.11`, p75 = `0.13`, p95 = `0.49`, p99 = `13.33`|
|||
|step name|`acquire_workspace_lock`|
|requests|total = `18`, ok = `18`, fail = `0`|
|RPS (req/sec)|total = `2`/s, ok = `2`/s, fail = `0`/s|
|latency (ms)|min = `0.03`, mean = `0.19`, max = `2.49`, StdDev = `0.56`|
|latency percentile (ms)|p50 = `0.04`, p75 = `0.06`, p95 = `0.1`, p99 = `2.49`|
|||
|step name|`mutate_workspace_file`|
|requests|total = `18`, ok = `18`, fail = `0`|
|RPS (req/sec)|total = `2`/s, ok = `2`/s, fail = `0`/s|
|latency (ms)|min = `0.05`, mean = `0.47`, max = `5.8`, StdDev = `1.3`|
|latency percentile (ms)|p50 = `0.09`, p75 = `0.22`, p95 = `0.54`, p99 = `5.8`|
|||
|step name|`commit_workspace`|
|requests|total = `18`, ok = `18`, fail = `0`|
|RPS (req/sec)|total = `2`/s, ok = `2`/s, fail = `0`/s|
|latency (ms)|min = `0.04`, mean = `0.41`, max = `5.77`, StdDev = `1.3`|
|latency percentile (ms)|p50 = `0.08`, p75 = `0.11`, p95 = `0.24`, p99 = `5.77`|
|||
|step name|`read_workspace_status`|
|requests|total = `18`, ok = `18`, fail = `0`|
|RPS (req/sec)|total = `2`/s, ok = `2`/s, fail = `0`/s|
|latency (ms)|min = `0.02`, mean = `0.34`, max = `5.08`, StdDev = `1.15`|
|latency percentile (ms)|p50 = `0.04`, p75 = `0.06`, p95 = `0.4`, p99 = `5.08`|


> status codes for scenario: `folder_workspace_full_lifecycle`



|status code|count|message|
|---|---|---|
|Accepted|72||
|Allowed|18||


