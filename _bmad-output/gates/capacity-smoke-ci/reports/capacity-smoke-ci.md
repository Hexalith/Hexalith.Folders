> test info



test suite: `nbomber_default_test_suite_name`

test name: `nbomber_default_test_name`

session id: `2026-09-19_09-45-54_a2f1b512`

> scenario stats



scenario: `folder_workspace_full_lifecycle`

  - duration: `00:00:03`

load simulations:

  - `inject`, rate: `1`, interval: `00:00:01`, during: `00:00:03`

|scenario and steps|ok stats|
|---|---|
|scenario name|`folder_workspace_full_lifecycle`|
|requests|total = `15`, ok = `15`, fail = `0`|
|RPS (req/sec)|total = `5`/s, ok = `5`/s, fail = `0`/s|
|latency (ms)|min = `0.06`, mean = `3.31`, max = `23.23`, StdDev = `5.99`|
|latency percentile (ms)|p50 = `0.2`, p75 = `2.93`, p95 = `8.07`, p99 = `23.25`|
|||
|step name|`prepare_workspace`|
|requests|total = `3`, ok = `3`, fail = `0`|
|RPS (req/sec)|total = `1`/s, ok = `1`/s, fail = `0`/s|
|latency (ms)|min = `0.2`, mean = `8.14`, max = `23.23`, StdDev = `10.68`|
|latency percentile (ms)|p50 = `0.99`, p75 = `0.99`, p95 = `23.25`, p99 = `23.25`|
|||
|step name|`acquire_workspace_lock`|
|requests|total = `3`, ok = `3`, fail = `0`|
|RPS (req/sec)|total = `1`/s, ok = `1`/s, fail = `0`/s|
|latency (ms)|min = `0.15`, mean = `1.1`, max = `2.93`, StdDev = `1.3`|
|latency percentile (ms)|p50 = `0.23`, p75 = `0.23`, p95 = `2.93`, p99 = `2.93`|
|||
|step name|`mutate_workspace_file`|
|requests|total = `3`, ok = `3`, fail = `0`|
|RPS (req/sec)|total = `1`/s, ok = `1`/s, fail = `0`/s|
|latency (ms)|min = `0.17`, mean = `2.2`, max = `6.24`, StdDev = `2.86`|
|latency percentile (ms)|p50 = `0.18`, p75 = `0.18`, p95 = `6.25`, p99 = `6.25`|
|||
|step name|`commit_workspace`|
|requests|total = `3`, ok = `3`, fail = `0`|
|RPS (req/sec)|total = `1`/s, ok = `1`/s, fail = `0`/s|
|latency (ms)|min = `0.1`, mean = `2.76`, max = `8.06`, StdDev = `3.75`|
|latency percentile (ms)|p50 = `0.11`, p75 = `0.11`, p95 = `8.07`, p99 = `8.07`|
|||
|step name|`read_workspace_status`|
|requests|total = `3`, ok = `3`, fail = `0`|
|RPS (req/sec)|total = `1`/s, ok = `1`/s, fail = `0`/s|
|latency (ms)|min = `0.06`, mean = `2.37`, max = `6.99`, StdDev = `3.26`|
|latency percentile (ms)|p50 = `0.07`, p75 = `0.07`, p95 = `6.99`, p99 = `6.99`|


> status codes for scenario: `folder_workspace_full_lifecycle`



|status code|count|message|
|---|---|---|
|Accepted|12||
|Allowed|3||


