> test info



test suite: `nbomber_default_test_suite_name`

test name: `nbomber_default_test_name`

session id: `2026-05-30_20-22-44_4b63ea15`

> scenario stats



scenario: `folder_workspace_full_lifecycle`

  - ok count: `18`

  - fail count: `0`

  - all data: `0` MB

  - duration: `00:00:09`

load simulations:

  - `inject`, rate: `2`, interval: `00:00:01`, during: `00:00:09`

|step|ok stats|
|---|---|
|name|`global information`|
|request count|all = `18`, ok = `18`, RPS = `2`|
|latency (ms)|min = `0.39`, mean = `3.83`, max = `57.66`, StdDev = `13.06`|
|latency percentile (ms)|p50 = `0.64`, p75 = `0.82`, p95 = `1.36`, p99 = `57.7`|
|||
|name|`prepare_workspace`|
|request count|all = `18`, ok = `18`, RPS = `2`|
|latency (ms)|min = `0.05`, mean = `0.71`, max = `11.02`, StdDev = `2.5`|
|latency percentile (ms)|p50 = `0.08`, p75 = `0.12`, p95 = `0.5`, p99 = `11.02`|
|||
|name|`acquire_workspace_lock`|
|request count|all = `18`, ok = `18`, RPS = `2`|
|latency (ms)|min = `0.02`, mean = `0.17`, max = `2.18`, StdDev = `0.49`|
|latency percentile (ms)|p50 = `0.03`, p75 = `0.06`, p95 = `0.19`, p99 = `2.18`|
|||
|name|`mutate_workspace_file`|
|request count|all = `18`, ok = `18`, RPS = `2`|
|latency (ms)|min = `0.04`, mean = `0.42`, max = `6.53`, StdDev = `1.48`|
|latency percentile (ms)|p50 = `0.05`, p75 = `0.08`, p95 = `0.14`, p99 = `6.53`|
|||
|name|`commit_workspace`|
|request count|all = `18`, ok = `18`, RPS = `2`|
|latency (ms)|min = `0.03`, mean = `0.34`, max = `5.18`, StdDev = `1.18`|
|latency percentile (ms)|p50 = `0.04`, p75 = `0.06`, p95 = `0.17`, p99 = `5.18`|
|||
|name|`read_workspace_status`|
|request count|all = `18`, ok = `18`, RPS = `2`|
|latency (ms)|min = `0.02`, mean = `0.28`, max = `4.43`, StdDev = `1.01`|
|latency percentile (ms)|p50 = `0.02`, p75 = `0.05`, p95 = `0.2`, p99 = `4.43`|


> status codes for scenario: `folder_workspace_full_lifecycle`



|status code|count|message|
|---|---|---|
|Accepted|72||
|Allowed|36||


