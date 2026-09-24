# Project decision policy

Effective 2026-09-24. Jerome is the sole project decision maker. This policy governs new product, architecture, security, delivery, planning, and release decisions in Hexalith.Folders. It replaces the multi-role attestation and human signature digest rules in earlier planning records for decisions made from this date onward. Those records remain evidence of what was decided under the prior process; their historical approvals are not rewritten.

## One owner decision

Present the proposed action, its scope, material risks, and the checks that must pass before execution. A clear response from Jerome in that context, including "I accept all" after an itemized proposal, accepts the stated scope and any stated conditions. Do not ask Jerome to repeat the same answer as separate Product, Security, Architecture, or Delivery personas, or to recite artifact hashes. Record one short decision note with the date, Jerome as decision maker, the proposal reference, the accepted scope, and any outstanding checks. An agent must not invent an acceptance or extend it to a materially different action.

An acceptance can authorize a conditional sequence. Complete each stated check before the dependent action, and record its result. A passing check is evidence, not another human signature. If a check fails, stop the dependent action and report the failure. Do not mark an unrun check as passed or an undeployed change as live.

## Changes after acceptance

Recheck generated hashes, tests, and deployment evidence when inputs change. Ask Jerome for another decision only when the intended behavior, risk, consumer scope, production exposure, or rollback plan changes materially beyond what was accepted. A byte change alone does not cancel an owner decision. Do not use a historical exact-digest approval as proof that a changed artifact passed its current technical checks.

Production timing and operations data can be supplied by the deployment owner without another approval round when they stay within the accepted window and limits. If a required value is unknown, mark it pending and do not execute the dependent step until it is supplied.

## Historical evidence and technical gates

Existing approval records, manifest digests, and exit-criteria fixtures remain historical evidence. Their content checks, security tests, contract parity, and release-readiness checks still apply. Their old role-by-role signatures, mandatory approval age, and human recitation of exact digests are not requirements for a new Jerome decision. Preserve provenance without making procedural digest churn a new approval gate.
