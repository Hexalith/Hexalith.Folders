# Story 1.17 Builds publication verification

From the Folders checkout that retains the publication receipt and bundle:

```bash
python3 eng/story117-builds-publication/verify.py
```

This checks the observed release/tag/source identity, successful release run,
release inventory digest and eligibility, signed package hashes and nuspec
source commits, the publisher's remote payload verification, retained signature
verification commands, all 37 exact synthetic acceptance expectations and both
actual-package seed subscription modes. The source fixture snapshots and all
command streams remain in the hash-bound portable bundle.

The synthetic validator records and Contract Sample Approver identities remain
synthetic. A passing validator control is not a Platform acceptance decision.
The 42 protected workspace files and 4,888 captured Platform inputs must still
match their captured bytes for the preservation check to pass.

The earlier dirty-source and clean-source diagnostic receipts remain historical.
Their authorization fields describe those observations, before publication was
authorized. No diagnostic archive is substituted for a published package, and
the diagnostic `4.31.2` bytes must never be uploaded under that released identity.

Publication authorization applies to Builds only. Platform acceptance, exact
tool pin/range and coherent gitlink, owner declaration/attestation and intake
prerequisites remain separate. Deployment and production activation remain
separately authorized. Preserve V1Only, unset T0, historical approvals, the 14
unexecuted staging checks and Story 1.17 backlog/open.

The release stdin test isolation follow-up changes the Builds source revision.
Its fresh clean-source qualification, retained diagnostic archives and exact-head
push CI are bound separately; earlier successful source receipts are not reused
for the changed inputs. The historical clean-source verifier intentionally
requires its original revision; use this publication verifier for the follow-up.

The hash report labels signed registry downloads, qualified release archives
and retained local diagnostics separately. The release inventory records the
qualified unsigned and symbol hashes. No independent symbol-package download
is claimed. The receipt supplies the exact observed release inspection command.
