#!/usr/bin/env bash
# Invoke from the Folders repository root with: run-with-temp bash <this-file>
set -u
: "${TMPDIR:?Run this verification through run-with-temp}"
evidence_dir="$PWD/_bmad-output/implementation-artifacts/evidence/story-11-6-reentry-2026-11-16"
# ParityOracle walks up from the assembly to the solution and reads canonical fixtures.
# Keep outputs disposable while linking to the unchanged source solution and tests.
ln -s "$PWD/Hexalith.Folders.slnx" "$TMPDIR/Hexalith.Folders.slnx"
ln -s "$PWD/tests" "$TMPDIR/tests"
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
    printf '%s tests exit %s\n' "$project_name" "$test_status"
    tail -12 "$evidence_dir/$project_name-final-tests.log"
    if test "$test_status" -ne 0; then
        overall_status=1
    fi
done
exit "$overall_status"
