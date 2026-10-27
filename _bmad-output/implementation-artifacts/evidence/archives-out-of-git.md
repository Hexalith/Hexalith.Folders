# Evidence archives kept out of git

From 2026-10-27, evidence archives (`*.tar.gz`) are no longer committed. They
made up most of the repository's size, and no test, CI job or gate reads them.
The committed records still identify each archive exactly: every receipt JSON
binds its archive's size and SHA-256, and every `*.members.json` lists the size
and SHA-256 of each archived file.

Archives committed before 2026-10-27 remain tracked in history.

## Where the archives are

The archives below exist only in the untracked `evidence/` folder of the
working copy that produced them. No other copy has been made.

| Archive | Bytes | SHA-256 |
| --- | ---: | --- |
| `story-1-17-current-builds-host-2026-10-26.artifacts.tar.gz` | 39,147,808 | `a29bc8d506cfe7094996a7951df0e4f43c1f6134983ced1a7bb17fba7211f638` |
| `story-1-17-current-builds-host-2026-10-26-attempt-2.artifacts.tar.gz` | 40,021,071 | `a3888979f64ed8b3f34ba938c8acff24271592fc9c1fd60101bf855721e8ebaf` |
| `story-1-17-current-builds-host-2026-10-26-attempt-2.diagnostic-inputs.artifacts.tar.gz` | 2,027,709 | `71ef57c5d15c78bbab51fc5f307765500cfc9123c801e4015668ecabb6a37633` |
| `story-1-17-current-builds-host-2026-10-26-attempt-3.artifacts.tar.gz` | 40,028,111 | `c84356a7bdb8a3a731587b9bf2e5264da9acad07c60c71c92b86f4919c3be72c` |
| `story-1-17-current-builds-host-2026-10-26-attempt-3.diagnostic-inputs.artifacts.tar.gz` | 5,443 | `a01073c1baac182b6940411592b21e3ae15193f992d851f38bddabf4619aed1b` |
| `story-1-17-current-builds-host-2026-10-26-attempt-4.artifacts.tar.gz` | 84,552,732 | `ebaea6b8a2e7a7430a17ce91499f4a4190007d8b2c769a707818b89a52296584` |
| `story-1-17-current-builds-host-2026-10-26-attempt-5.artifacts.tar.gz` | 144,962,765 | `90443dbf6902c05d96542eeaa013f613c8b38d38c895c346910e2cd1cc879dce` |
| `story-1-17-current-builds-host-2026-10-26.post-review-supplement.artifacts.tar.gz` | 4,317,135 | `f8adee3be45b0d3a598bc4d4508c847e9cb7770cce446844dc864577ae764c57` |
| `story-1-17-current-builds-host-2026-10-26.supporting.artifacts.tar.gz` | 831,811 | `06793d8cf6cebf3a8a3b66fef0ed8231d2d77ad4a41d2b6c5530eb15e6985ba5` |

## Verifying an archive

Results documents may show a `verify.py` command that reads an archive from
this folder. That command works only after the archive is copied back to its
recorded path. Check the copy with `sha256sum` against the table above or the
receipt's `archive.sha256` first.
