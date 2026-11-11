> test info



test suite: `nbomber_default_test_suite_name`

test name: `nbomber_default_test_name`

session id: `2026-11-11_21-17-27_60e6c656`

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
|latency (ms)|min = `0.02`, mean = `0.49`, max = `12.74`, StdDev = `1.86`|
|latency percentile (ms)|p50 = `0.09`, p75 = `0.15`, p95 = `2.31`, p99 = `11.05`|
|||
|step name|`prepare_workspace`|
|requests|total = `18`, ok = `18`, fail = `0`|
|RPS (req/sec)|total = `2`/s, ok = `2`/s, fail = `0`/s|
|latency (ms)|min = `0.07`, mean = `0.85`, max = `12.74`, StdDev = `2.89`|
|latency percentile (ms)|p50 = `0.12`, p75 = `0.16`, p95 = `0.42`, p99 = `12.74`|
|||
|step name|`acquire_workspace_lock`|
|requests|total = `18`, ok = `18`, fail = `0`|
|RPS (req/sec)|total = `2`/s, ok = `2`/s, fail = `0`/s|
|latency (ms)|min = `0.03`, mean = `0.2`, max = `2.31`, StdDev = `0.51`|
|latency percentile (ms)|p50 = `0.06`, p75 = `0.11`, p95 = `0.22`, p99 = `2.31`|
|||
|step name|`mutate_workspace_file`|
|requests|total = `18`, ok = `18`, fail = `0`|
|RPS (req/sec)|total = `2`/s, ok = `2`/s, fail = `0`/s|
|latency (ms)|min = `0.1`, mean = `0.78`, max = `11.04`, StdDev = `2.49`|
|latency percentile (ms)|p50 = `0.16`, p75 = `0.2`, p95 = `0.36`, p99 = `11.05`|
|||
|step name|`commit_workspace`|
|requests|total = `18`, ok = `18`, fail = `0`|
|RPS (req/sec)|total = `2`/s, ok = `2`/s, fail = `0`/s|
|latency (ms)|min = `0.05`, mean = `0.38`, max = `5.27`, StdDev = `1.19`|
|latency percentile (ms)|p50 = `0.08`, p75 = `0.13`, p95 = `0.2`, p99 = `5.27`|
|||
|step name|`read_workspace_status`|
|requests|total = `18`, ok = `18`, fail = `0`|
|RPS (req/sec)|total = `2`/s, ok = `2`/s, fail = `0`/s|
|latency (ms)|min = `0.02`, mean = `0.26`, max = `3.89`, StdDev = `0.88`|
|latency percentile (ms)|p50 = `0.03`, p75 = `0.04`, p95 = `0.18`, p99 = `3.89`|


> status codes for scenario: `folder_workspace_full_lifecycle`



|status code|count|message|
|---|---|---|
|Accepted|72||
|Allowed|18||


