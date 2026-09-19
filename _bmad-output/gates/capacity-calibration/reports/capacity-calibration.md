> test info



test suite: `nbomber_default_test_suite_name`

test name: `nbomber_default_test_name`

session id: `2026-09-19_09-46-09_686106e6`

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
|latency (ms)|min = `0.03`, mean = `1.14`, max = `38.54`, StdDev = `4.8`|
|latency percentile (ms)|p50 = `0.12`, p75 = `0.19`, p95 = `10.88`, p99 = `17.98`|
|||
|step name|`prepare_workspace`|
|requests|total = `18`, ok = `18`, fail = `0`|
|RPS (req/sec)|total = `2`/s, ok = `2`/s, fail = `0`/s|
|latency (ms)|min = `0.13`, mean = `2.37`, max = `38.54`, StdDev = `8.78`|
|latency percentile (ms)|p50 = `0.19`, p75 = `0.22`, p95 = `1.06`, p99 = `38.56`|
|||
|step name|`acquire_workspace_lock`|
|requests|total = `18`, ok = `18`, fail = `0`|
|RPS (req/sec)|total = `2`/s, ok = `2`/s, fail = `0`/s|
|latency (ms)|min = `0.04`, mean = `0.69`, max = `10.88`, StdDev = `2.47`|
|latency percentile (ms)|p50 = `0.09`, p75 = `0.11`, p95 = `0.18`, p99 = `10.88`|
|||
|step name|`mutate_workspace_file`|
|requests|total = `18`, ok = `18`, fail = `0`|
|RPS (req/sec)|total = `2`/s, ok = `2`/s, fail = `0`/s|
|latency (ms)|min = `0.08`, mean = `1.15`, max = `17.97`, StdDev = `4.08`|
|latency percentile (ms)|p50 = `0.16`, p75 = `0.2`, p95 = `0.27`, p99 = `17.98`|
|||
|step name|`commit_workspace`|
|requests|total = `18`, ok = `18`, fail = `0`|
|RPS (req/sec)|total = `2`/s, ok = `2`/s, fail = `0`/s|
|latency (ms)|min = `0.05`, mean = `0.77`, max = `11.51`, StdDev = `2.61`|
|latency percentile (ms)|p50 = `0.1`, p75 = `0.19`, p95 = `0.44`, p99 = `11.51`|
|||
|step name|`read_workspace_status`|
|requests|total = `18`, ok = `18`, fail = `0`|
|RPS (req/sec)|total = `2`/s, ok = `2`/s, fail = `0`/s|
|latency (ms)|min = `0.03`, mean = `0.7`, max = `11.32`, StdDev = `2.58`|
|latency percentile (ms)|p50 = `0.06`, p75 = `0.11`, p95 = `0.16`, p99 = `11.33`|


> status codes for scenario: `folder_workspace_full_lifecycle`



|status code|count|message|
|---|---|---|
|Accepted|72||
|Allowed|18||


