#!/usr/bin/env bash
# Invoke from the Folders repository root with: run-with-temp bash <this-file>
set -uo pipefail
: "${TMPDIR:?Run this verification through run-with-temp}"
evidence_dir="${1:-$PWD/_bmad-output/implementation-artifacts/evidence/story-11-6-reentry-2026-11-16}"
mkdir -p -- "$evidence_dir" || exit 2
# A rerun needs a new output directory to preserve existing receipt-bound logs.
for project_name in Hexalith.Folders.Cli.Tests Hexalith.Folders.Mcp.Tests Hexalith.Folders.IntegrationTests; do
    for suffix in build tests; do
        if test -e "$evidence_dir/$project_name-final-$suffix.log"; then
            printf 'Refusing to overwrite retained evidence: %s\n' "$evidence_dir/$project_name-final-$suffix.log" >&2
            exit 2
        fi
    done
done
# ParityOracle walks up from the assembly to the solution and reads canonical fixtures.
# Keep outputs disposable while linking to the unchanged source solution and tests.
ln -s "$PWD/Hexalith.Folders.slnx" "$TMPDIR/Hexalith.Folders.slnx" || exit 2
ln -s "$PWD/tests" "$TMPDIR/tests" || exit 2
overall_status=0
for project_name in Hexalith.Folders.Cli.Tests Hexalith.Folders.Mcp.Tests Hexalith.Folders.IntegrationTests; do
    printf 'Building %s\n' "$project_name"
    dotnet build "tests/$project_name/$project_name.csproj" --configuration Debug -p:UseNuGetDeps=false --artifacts-path "$TMPDIR/artifacts" -m:1 > "$evidence_dir/$project_name-final-build.log" 2>&1
    build_status=$?
    printf '%s build exit %s\n' "$project_name" "$build_status"
    if test "$build_status" -ne 0; then
        tail -30 "$evidence_dir/$project_name-final-build.log"
        overall_status=1
        continue
    fi
    assembly_path="$TMPDIR/artifacts/bin/$project_name/debug/$project_name.dll"
    if test "$project_name" = Hexalith.Folders.IntegrationTests; then
        dotnet "$assembly_path" -class '*CrossAdapterBehavioralParityTests' > "$evidence_dir/$project_name-final-tests.log" 2>&1
    else
        dotnet "$assembly_path" > "$evidence_dir/$project_name-final-tests.log" 2>&1
    fi
    test_status=$?
    if test "$test_status" -eq 0 && ! rg -q 'Total: [1-9][0-9]*, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0,' "$evidence_dir/$project_name-final-tests.log"; then
        printf 'Missing positive, complete test summary for %s\n' "$project_name" >&2
        test_status=1
    fi
    printf '%s tests exit %s\n' "$project_name" "$test_status"
    tail -12 "$evidence_dir/$project_name-final-tests.log"
    if test "$test_status" -ne 0; then
        overall_status=1
    fi
done
exit "$overall_status"
