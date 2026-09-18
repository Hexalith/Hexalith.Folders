#!/usr/bin/env bash
set -euo pipefail

version="${1:-}"
phase="${2:-}"
expected_builds_sha="b93e9889e9e7b67036837015b4b2b115e326c4da"
expected_package_count=5
manifest="${HEXALITH_RELEASE_PACKAGE_MANIFEST:-}"
source_sha="${GITHUB_SHA:-}"

fail() {
  echo "[publication-preflight] $1" >&2
  exit 1
}

[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] || fail "A plain semantic release version is required."
[[ "$phase" =~ ^(verify|publish)$ ]] || fail "Publication phase must be verify or publish."
[[ "$source_sha" =~ ^[0-9a-f]{40}$ ]] || fail "GITHUB_SHA must identify the exact release source."
[[ "${HEXALITH_BUILDS_EXECUTION_SHA:-}" = "$expected_builds_sha" ]] || fail "The approved Builds execution identity does not match the immutable release workflow pin."
[[ "${HEXALITH_RELEASE_SOURCE_BRANCH:-}" = "main" ]] || fail "The release source branch must be exactly main."
[[ "${HEXALITH_RELEASE_SOURCE_CI_WORKFLOW:-}" = "ci.yml" ]] || fail "The exact-source proof must use ci.yml."
[[ "${HEXALITH_RELEASE_ENVIRONMENT:-}" = "production" ]] || fail "The protected release environment must be production."
[[ "${HEXALITH_RELEASE_EXPECTED_PACKAGE_COUNT:-}" = "$expected_package_count" ]] || fail "The caller-declared package count must be exactly five."
[[ "${HEXALITH_RELEASE_REQUIRE_AUTHORITY:-}" = "false" ]] || fail "Folders releases use protected-environment approval as publication authority; the reusable-workflow authority mode must be explicit."
[[ "$manifest" = "tools/release-packages.json" && -f "$manifest" ]] || fail "The authoritative package manifest is unavailable."
[[ -n "${GITHUB_TOKEN:-}" ]] || fail "GITHUB_TOKEN is required for exact-source revalidation."
[[ -n "${GITHUB_REPOSITORY:-}" ]] || fail "GITHUB_REPOSITORY is required for exact-source revalidation."

manifest_count="$(jq -er '.packages | if type == "array" then length else error("packages must be an array") end' "$manifest")"
[[ "$manifest_count" = "$expected_package_count" ]] || fail "The package manifest does not contain exactly five packages."

live_main_sha="$(gh api "repos/${GITHUB_REPOSITORY}/git/ref/heads/main" --jq '.object.sha')"
[[ "$live_main_sha" =~ ^[0-9a-f]{40}$ && "$live_main_sha" = "$source_sha" ]] || fail "The release source is no longer the exact live main tip."

ci_runs="$(gh api --method GET \
  "repos/${GITHUB_REPOSITORY}/actions/workflows/ci.yml/runs" \
  -f branch=main \
  -f event=push \
  -f head_sha="$source_sha" \
  -f status=success \
  -f per_page=100)"
printf '%s\n' "$ci_runs" | jq -e --arg sha "$source_sha" '
  [.workflow_runs[] | select(
    .head_sha == $sha and
    .head_branch == "main" and
    .event == "push" and
    .status == "completed" and
    .conclusion == "success"
  )] | length > 0
' >/dev/null || fail "No successful push CI run exists for the exact current main SHA."

while IFS= read -r package_id; do
  [[ "$package_id" =~ ^[A-Za-z0-9][A-Za-z0-9._-]*$ ]] || fail "The package manifest contains an invalid package ID."
  package_index="https://api.nuget.org/v3-flatcontainer/$(printf '%s' "$package_id" | tr '[:upper:]' '[:lower:]')/index.json"
  response_file="$(mktemp)"
  trap 'rm -f "$response_file"' EXIT
  status="$(curl --silent --show-error --location --max-time 30 --retry 3 --retry-all-errors \
    --output "$response_file" --write-out '%{http_code}' "$package_index")"
  case "$status" in
    404) ;;
    200)
      if jq -e --arg version "$version" '.versions | index($version) != null' "$response_file" >/dev/null; then
        fail "NuGet.org already contains ${package_id} ${version}; duplicate versions are immutable and are never skipped."
      fi
      ;;
    *) fail "NuGet.org returned unexpected status ${status} for ${package_id}." ;;
  esac
  rm -f "$response_file"
  trap - EXIT
done < <(jq -er '.packages[].id' "$manifest")

if [ "$phase" = "publish" ]; then
  tag_commit="$(git rev-parse "v${version}^{commit}")"
  [ "$tag_commit" = "$source_sha" ] || fail "The semantic-release tag does not target the approved source."
fi
