# Historical configuration evidence

The eight files in [manifest.json](manifest.json) preserve inactive historical NuGet inputs as deterministic gzip data. No active build configuration, audit rule or original receipt changed. Each archive decompresses to its original Git blob; the manifest preserves the original path, mode, size and SHA-256.

Historical receipts still name the original paths. Reconstruct their exact layout only under the private TMPDIR of run-with-temp when inspecting or replaying them. These archives are evidence, not inputs for active restore, build or release.
