#!/usr/bin/env bash
# Consumer recheck of the merged Builds/Projects tuple. Mirrors the commands recorded in
# story-1-17-published-release-2026-11-15/completed-commands.json (Release/package, NuGet.org only)
# and story-1-17-post-eventstore-511-recheck-2026-11-15/final/commands.json (Debug/source).
set -u
OUT=/tmp/claude-1000/-home-administrator-projects-hexalith-folders/86c86754-f205-4744-894b-86d279bdcb7e/scratchpad/recheck-merged
ROOT=/home/administrator/projects/hexalith/folders
PROJ=$ROOT/references/Hexalith.Projects
export PATH="$HOME/.dotnet/tools:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
CFG=$OUT/NuGet.Config
cat > "$CFG" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF
LEDGER=$OUT/ledger.tsv
: > "$LEDGER"
AUDIT=(-p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=low)
PKG=(-p:UseHexalithProjectReferences=false)
SRC=(-p:UseHexalithProjectReferences=true -p:UseNuGetDeps=false)

run() { # label cwd cmd...
  local label=$1 cwd=$2; shift 2
  local start=$(date +%s)
  (cd "$cwd" && "$@") > "$OUT/$label.log" 2>&1
  local rc=$?
  printf '%s\t%s\t%ss\t%s\n' "$label" "$rc" "$(( $(date +%s) - start ))" "$*" >> "$LEDGER"
  return $rc
}

# Projects, Release/package
run projects-restore "$PROJ" dotnet restore Hexalith.Projects.CI.slnx -p:Configuration=Release "${PKG[@]}" "${AUDIT[@]}" --configfile "$CFG" -m:1 --disable-build-servers &&
run projects-release-build "$PROJ" dotnet build Hexalith.Projects.CI.slnx -c Release "${PKG[@]}" "${AUDIT[@]}" -m:1 -warnaserror --disable-build-servers --no-restore &&
run projects-adapters "$PROJ" dotnet "$PROJ/tests/Hexalith.Projects.Server.Tests/bin/Release/net11.0/Hexalith.Projects.Server.Tests.dll" -noLogo -noColor \
  -class Hexalith.Projects.Server.Tests.ProjectFolderDirectoryTests -class Hexalith.Projects.Server.Tests.ProjectFileReferenceDirectoryTests \
  -class Hexalith.Projects.Server.Tests.DelegatedProjectDirectoryRegistrationTests -class 'Hexalith.Projects.Server.Tests.Conversations.*' \
  -class 'Hexalith.Projects.Server.Tests.Folders.*' -class Hexalith.Projects.Server.Tests.ProjectConversationAssignmentEndpointTests \
  -result-xml "$OUT/projects-adapters.xml"

# Folders, Release/package
run folders-restore "$ROOT" dotnet restore Hexalith.Folders.CI.slnx -p:Configuration=Release "${PKG[@]}" "${AUDIT[@]}" --configfile "$CFG" -m:1 --disable-build-servers &&
run folders-release-build "$ROOT" dotnet build Hexalith.Folders.CI.slnx -c Release "${PKG[@]}" "${AUDIT[@]}" -m:1 -warnaserror --disable-build-servers --no-restore && {
  run folders-native-authority "$ROOT" dotnet "$ROOT/tests/Hexalith.Folders.Server.Tests/bin/Release/net11.0/Hexalith.Folders.Server.Tests.dll" -noLogo -noColor \
    -class Hexalith.Folders.Server.Tests.NativeSourceHeadReaderTests -class Hexalith.Folders.Server.Tests.FoldersNativeGatewayBearerTests \
    -class Hexalith.Folders.Server.Tests.WorkspaceContentProductionTests -class Hexalith.Folders.Server.Tests.WorkspaceContentRejectionMappingTests \
    -class Hexalith.Folders.Server.Tests.WorkspaceContentErasureGrantTests -result-xml "$OUT/folders-native-authority.xml"
  run folders-routing "$ROOT" dotnet "$ROOT/tests/Hexalith.Folders.IntegrationTests/bin/Release/net11.0/Hexalith.Folders.IntegrationTests.dll" -noLogo -noColor \
    -class Hexalith.Folders.IntegrationTests.Routing.FoldersApiRoutingModeTests -class Hexalith.Folders.IntegrationTests.WorkspaceContentProcessWiringTests \
    -result-xml "$OUT/folders-routing.xml"
  run folders-parity "$ROOT" pwsh -NoLogo -NoProfile -File tests/tools/run-contract-parity-ci-gates.ps1 -SkipRestoreBuild -OutputReportPath "$OUT/parity-report.json"
  run folders-governance "$ROOT" pwsh -NoLogo -NoProfile -File tests/tools/run-governance-completeness-gates.ps1 -SkipRestoreBuild
}

# Folders, Debug/source focused checks
T=tests
for p in Hexalith.Folders.Server.Tests Hexalith.Folders.IntegrationTests Hexalith.Folders.Contracts.Tests; do
  run "source-$p-restore" "$ROOT" dotnet restore "$T/$p/$p.csproj" -p:Configuration=Debug --configfile "$CFG" "${SRC[@]}" -m:1 --disable-build-servers &&
  run "source-$p-build" "$ROOT" dotnet build "$T/$p/$p.csproj" --configuration Debug --no-restore -warnaserror "${SRC[@]}" -m:1 --disable-build-servers
done
D=bin/Debug/net11.0
run source-native-authority "$ROOT" dotnet "$ROOT/$T/Hexalith.Folders.Server.Tests/$D/Hexalith.Folders.Server.Tests.dll" -noLogo -noColor \
  -class Hexalith.Folders.Server.Tests.NativeSourceHeadReaderTests -class Hexalith.Folders.Server.Tests.FoldersNativeGatewayBearerTests \
  -class Hexalith.Folders.Server.Tests.WorkspaceContentProductionTests -class Hexalith.Folders.Server.Tests.WorkspaceContentRejectionMappingTests \
  -class Hexalith.Folders.Server.Tests.WorkspaceContentErasureGrantTests -result-xml "$OUT/source-native-authority.xml"
run source-routing "$ROOT" dotnet "$ROOT/$T/Hexalith.Folders.IntegrationTests/$D/Hexalith.Folders.IntegrationTests.dll" -noLogo -noColor \
  -class Hexalith.Folders.IntegrationTests.Routing.FoldersApiRoutingModeTests -class Hexalith.Folders.IntegrationTests.WorkspaceContentProcessWiringTests \
  -result-xml "$OUT/source-routing.xml"
run source-contract-behavior "$ROOT" dotnet "$ROOT/$T/Hexalith.Folders.Contracts.Tests/$D/Hexalith.Folders.Contracts.Tests.dll" -noLogo -noColor \
  -class Hexalith.Folders.Contracts.Tests.OpenApi.AuthorizationMatrixContractTests -class Hexalith.Folders.Contracts.Tests.OpenApi.Pd10V2CandidateContractTests \
  -class Hexalith.Folders.Contracts.Tests.OpenApi.ContractSpineCiGateTests -result-xml "$OUT/source-contract-behavior.xml"
run source-conformance "$ROOT" dotnet "$ROOT/$T/Hexalith.Folders.Contracts.Tests/$D/Hexalith.Folders.Contracts.Tests.dll" -noLogo -noColor \
  -class Hexalith.Folders.Contracts.Tests.OpenApi.Pd10ConformanceSetTests -result-xml "$OUT/source-conformance.xml"
run source-deployment-conformance "$ROOT" dotnet "$ROOT/$T/Hexalith.Folders.Contracts.Tests/$D/Hexalith.Folders.Contracts.Tests.dll" -noLogo -noColor \
  -class Hexalith.Folders.Contracts.Tests.Deployment.ContainerImageConformanceTests -class Hexalith.Folders.Contracts.Tests.Deployment.ReleasePackageConformanceTests \
  -result-xml "$OUT/source-deployment-conformance.xml"

# Folders, full Debug/source CI solution
run source-solution-restore "$ROOT" dotnet restore Hexalith.Folders.CI.slnx -p:Configuration=Debug --configfile "$CFG" "${SRC[@]}" -m:1 --disable-build-servers &&
run source-solution-build "$ROOT" dotnet build Hexalith.Folders.CI.slnx --configuration Debug --no-restore -warnaserror "${SRC[@]}" -m:1 --disable-build-servers

echo "DONE" >> "$LEDGER"
