#!/usr/bin/env bash
set -euo pipefail

readonly version=1.18.0
readonly archive_sha256=2a94739e0aa101289d88418225319562bc6800db273b3d9cf819a0efd1ea1bfe
readonly archive="${RUNNER_TEMP:?RUNNER_TEMP is required}/dapr_linux_amd64-${version}.tar.gz"
readonly install_directory="${RUNNER_TEMP}/dapr-cli-${version}"

curl --fail --show-error --silent --location --proto '=https' --tlsv1.2 \
  "https://github.com/dapr/cli/releases/download/v${version}/dapr_linux_amd64.tar.gz" \
  --output "$archive"
printf '%s  %s\n' "$archive_sha256" "$archive" | sha256sum --check --strict
mkdir -p "$install_directory"
tar -xzf "$archive" -C "$install_directory" dapr
printf '%s\n' "$install_directory" >> "${GITHUB_PATH:?GITHUB_PATH is required}"
"$install_directory/dapr" --version
