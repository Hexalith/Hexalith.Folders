#!/usr/bin/env bash
set -euo pipefail

fail() {
    echo "[ci-topology] $1" >&2
    exit 1
}

[[ "${GITHUB_ACTIONS:-}" = true && "${GITHUB_SERVER_URL:-}" = https://github.com &&
   "${GITHUB_REPOSITORY:-}" = Hexalith/Hexalith.Folders ]] ||
    fail "Preparation requires the GitHub Hexalith/Hexalith.Folders workflow."
[[ "${GITHUB_WORKSPACE:-}" = /* && -d "$GITHUB_WORKSPACE" ]] || fail "An absolute GitHub workspace is required."
workspace="$(cd -- "$GITHUB_WORKSPACE" && pwd -P)"
script_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
[[ "$GITHUB_WORKSPACE" = "$workspace" && "$(pwd -P)" = "$workspace" && "$script_root" = "$workspace" ]] ||
    fail "Run the checked-out script from the exact GitHub workspace root."
[[ -d .git && ! -L .git && -f Hexalith.Folders.slnx ]] || fail "A standalone Folders Git root is required."
for variable in GIT_DIR GIT_WORK_TREE GIT_COMMON_DIR GIT_INDEX_FILE; do
    [[ -z "${!variable:-}" ]] || fail "Git directory and index overrides are forbidden."
done
[[ "$(git rev-parse --show-toplevel)" = "$workspace" ]] || fail "The Git root must equal the GitHub workspace."
superproject="$(git rev-parse --show-superproject-working-tree)" || fail "Cannot determine whether this checkout has a superproject."
[[ -z "$superproject" ]] || fail "Submodule/superproject checkouts are forbidden."
ancestor="$(dirname -- "$workspace")"
while :; do
    [[ ! -e "$ancestor/.git" && ! -L "$ancestor/.git" ]] || fail "Nested repository checkouts are forbidden."
    [[ "$ancestor" = / ]] && break
    ancestor="$(dirname -- "$ancestor")"
done
origin="$(git remote get-url origin)"
[[ "$origin" = https://github.com/Hexalith/Hexalith.Folders ||
   "$origin" = https://github.com/Hexalith/Hexalith.Folders.git ]] || fail "The origin must be Hexalith/Hexalith.Folders."

module_path=references/Hexalith.Memories
module_url=https://github.com/Hexalith/Hexalith.Memories.git
[[ ! -L references && ! -L "$module_path" ]] || fail "The root dependency path must not traverse symbolic links."
[[ -f .gitmodules && ! -L .gitmodules ]] || fail "The root submodule declaration is missing."
git cat-file -e HEAD:.gitmodules || fail "The root declaration must be recorded in HEAD."
git diff --quiet HEAD -- .gitmodules || fail "The root declaration has uncommitted changes."
[[ "$(git config --file .gitmodules --get-all submodule.Hexalith.Memories.path)" = "$module_path" &&
   "$(git config --file .gitmodules --get-all submodule.Hexalith.Memories.url)" = "$module_url" ]] ||
    fail "The root Memories path or URL declaration is missing or changed."
[[ "$(git config --file .gitmodules --get-regexp '^submodule\..*\.path$' | awk -v path="$module_path" '$2 == path { count++ } END { print count+0 }')" = 1 ]] ||
    fail "The Memories path must have exactly one root declaration."
configured_url="$(git config --get submodule.Hexalith.Memories.url || true)"
[[ -z "$configured_url" || "$configured_url" = "$module_url" ]] || fail "The local Memories URL differs from the root declaration."
read -r mode object_type recorded_sha recorded_path <<< "$(git ls-tree HEAD -- "$module_path")"
[[ "$mode" = 160000 && "$object_type" = commit && "$recorded_sha" =~ ^[0-9a-f]{40}$ && "$recorded_path" = "$module_path" ]] ||
    fail "Memories must be a root gitlink recorded in HEAD."
git diff --cached --quiet HEAD -- "$module_path" || fail "The Memories gitlink has staged changes."

# The workflow owns this explicit root prerequisite. Never initialize from MSBuild
# or follow the module's own references; checkout uses the recorded gitlink only.
git -c submodule.recurse=false -c fetch.recurseSubmodules=false submodule update --init --checkout -- "$module_path"
[[ "$(git -C "$module_path" rev-parse HEAD)" = "$recorded_sha" ]] || fail "The Memories checkout differs from the recorded gitlink."
selected_status="$(git -C "$module_path" status --porcelain --untracked-files=all)" || fail "Cannot inspect the Memories checkout worktree."
[[ -z "$selected_status" ]] || fail "The selected Memories checkout must be clean, including untracked metadata."
[[ -f "$module_path/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj" ]] || fail "Memories server project metadata is unavailable."
echo "[ci-topology] Prepared $module_path at $recorded_sha."
