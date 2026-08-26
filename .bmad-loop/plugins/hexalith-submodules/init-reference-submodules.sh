#!/usr/bin/env bash
set -euo pipefail

worktree="${BMAD_LOOP_WORKTREE:?BMAD_LOOP_WORKTREE is required}"
gitmodules="$worktree/.gitmodules"

if [[ ! -f "$gitmodules" ]]; then
    echo "Root .gitmodules not found: $gitmodules" >&2
    exit 1
fi

reference_submodules=()

while IFS= read -r -d '' entry; do
    path="${entry#*$'\n'}"

    case "$path" in
        references/*)
            reference_submodules+=("$path")
            ;;
    esac
done < <(
    git -C "$worktree" config \
        --null \
        --file .gitmodules \
        --get-regexp '^submodule\..*\.path$'
)

if (( ${#reference_submodules[@]} == 0 )); then
    echo "No root-declared submodules found under references/" >&2
    exit 1
fi

git -C "$worktree" submodule update \
    --init \
    -- \
    "${reference_submodules[@]}"
