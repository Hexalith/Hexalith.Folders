---
title: 'Run the archive integration regressions in CI'
type: 'bugfix'
created: '2026-11-23'
status: 'done'
route: 'oneshot'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Close DW-305, DW-306, DW-307, DW-308, and DW-316 through the existing archive integration harness. Run the integration assembly in the blocking baseline lane, verify the replay flag across REST and SDK, cover the archive policy outcomes that reach the REST boundary, and distinguish the safe policy classifier from scoped metadata.

</frozen-after-approval>

## Implementation Notes

- The baseline gate invokes the full Release xUnit assembly. The existing live-runtime opt-in still governs those probes.
- The checked-in baseline report remains historical evidence. The script conformance test pins the active integration lane, and report validation accepts the previous inventory.
- The archive policy matrix checks the /process rejection code, public Problem Details, retryability, and persisted folder state. The REST and SDK legs assert the first-write and replay flags.

## Review Triage Log

- Medium, fixed. Restrict the classifier exemption to a top-level details.policyClass string equal to mutation.
- Medium, fixed. Assert the /process rejection code separately from the public safe-denial contract.
- Low, fixed. Run the scoped metadata scan once per response.
- Medium, fixed. Pin the unfiltered integration runner arguments and verify the complete assembly.

## Verification

- Release builds of the IntegrationTests and Contracts.Tests projects passed with no warnings or errors.
- The full Release integration assembly passed 865 tests. The three affected integration classes passed 84 tests in a focused run.
- BaselineCiWorkflowConformanceTests passed all 13 tests. The focused integration whitespace check passed.
- The complete PowerShell gate and live Aspire topology were not run in this environment. PowerShell and the Aspire CLI were unavailable.
