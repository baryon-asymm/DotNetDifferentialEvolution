#!/usr/bin/env bash
# The release gate, after APThermo's script of the same name (as read on 2026-10-05, its commit ea62488c):
# - a tag `<prefix><version>` must equal the packed version (skipped when no ref is given, as on
#   workflow_dispatch); the prefix is `v` for the CPU package and `gpu-v` for the GPU package;
# - the changelog must carry a non-empty `## <version>` section (`## [<version>]` also accepted),
#   extracted into a notes file for the GitHub release.
#
# Usage: check-release.sh <version> <tag-prefix> <changelog-file> <notes-output-file> [<ref-name>]
set -euo pipefail

version=$1
prefix=$2
changelog=$3
notes_out=$4
ref_name=${5:-}

if [[ -n "$ref_name" ]]; then
  expected="${prefix}${version}"
  if [[ "$ref_name" != "$expected" ]]; then
    echo "::error::tag '${ref_name}' does not match the package version '${expected}'" >&2
    exit 1
  fi
fi

if [[ ! -f "$changelog" ]]; then
  echo "::error::changelog not found: ${changelog}" >&2
  exit 1
fi

# A section runs from its `## ` heading to the next `## ` heading; `### ` subsections stay inside.
awk -v ver="$version" '
  BEGIN { found = 0 }
  /^## / {
    if (found) { exit }
    heading = substr($0, 4)
    if (heading == ver || heading == "[" ver "]" || index(heading, "[" ver "] ") == 1 || index(heading, ver " ") == 1) {
      found = 1
    }
    next
  }
  found { print }
' "$changelog" > "$notes_out"

# Drop leading and trailing blank lines without touching the notes themselves.
sed -i -e '/./,$!d' -e ':a' -e '/^\n*$/{$d;N;ba' -e '}' "$notes_out"

if [[ ! -s "$notes_out" ]]; then
  echo "::error::${changelog} has no non-empty '## ${version}' section" >&2
  exit 1
fi

echo "release notes for ${prefix}${version} extracted to ${notes_out}:"
cat "$notes_out"
