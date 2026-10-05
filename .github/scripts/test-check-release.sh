#!/usr/bin/env bash
# Self-tests of check-release.sh, run by ci.yml. Each case states the outcome it expects; the
# script under test is never asked what the answer is.
#
# Usage: test-check-release.sh   (from any directory)
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
gate="$here/check-release.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

expect_pass() {
  local name=$1; shift
  if bash "$gate" "$@" > "$work/out" 2>&1; then
    echo "ok   $name"
  else
    echo "FAIL $name: expected success"; cat "$work/out"; failures=$((failures + 1))
  fi
}

expect_fail() {
  local name=$1 message=$2; shift 2
  if bash "$gate" "$@" > "$work/out" 2>&1; then
    echo "FAIL $name: expected failure"; failures=$((failures + 1))
  elif ! grep -qF "$message" "$work/out"; then
    echo "FAIL $name: the error does not say '$message'"; cat "$work/out"; failures=$((failures + 1))
  else
    echo "ok   $name"
  fi
}

expect_notes() {
  local name=$1 expected=$2
  if [[ "$(cat "$work/notes.md")" == "$expected" ]]; then
    echo "ok   $name"
  else
    echo "FAIL $name: notes were"; cat "$work/notes.md"; failures=$((failures + 1))
  fi
}

cat > "$work/CHANGELOG.md" <<'EOF'
# Changelog

Preamble.

## 2.1.0

First line of 2.1.0.

### Breaking

- An item of 2.1.0.


## [2.0.0] - 2026-01-01

Notes of 2.0.0.

## 1.0.0

## 0.9.0

Notes of 0.9.0.
EOF

expect_pass "a CPU tag equal to the version" 2.1.0 v "$work/CHANGELOG.md" "$work/notes.md" v2.1.0
expect_notes "the section runs to the next '## ' heading, '### ' kept, blank edges dropped" \
  "$(printf 'First line of 2.1.0.\n\n### Breaking\n\n- An item of 2.1.0.')"

expect_pass "a GPU tag equal to the version" 2.1.0 gpu-v "$work/CHANGELOG.md" "$work/notes.md" gpu-v2.1.0
expect_pass "no ref (workflow_dispatch) skips the tag check" 2.1.0 v "$work/CHANGELOG.md" "$work/notes.md"
expect_pass "a bracketed heading with a date" 2.0.0 v "$work/CHANGELOG.md" "$work/notes.md" v2.0.0
expect_notes "the bracketed section's notes" "Notes of 2.0.0."

expect_fail "a tag of another version" "does not match the package version 'v2.1.0'" \
  2.1.0 v "$work/CHANGELOG.md" "$work/notes.md" v2.1.1
expect_fail "a CPU tag for the GPU package" "does not match the package version 'gpu-v2.1.0'" \
  2.1.0 gpu-v "$work/CHANGELOG.md" "$work/notes.md" v2.1.0
expect_fail "a GPU tag for the CPU package" "does not match the package version 'v2.1.0'" \
  2.1.0 v "$work/CHANGELOG.md" "$work/notes.md" gpu-v2.1.0
expect_fail "an empty section" "has no non-empty '## 1.0.0' section" \
  1.0.0 v "$work/CHANGELOG.md" "$work/notes.md" v1.0.0
expect_fail "a version with no section" "has no non-empty '## 3.0.0' section" \
  3.0.0 v "$work/CHANGELOG.md" "$work/notes.md" v3.0.0
expect_fail "a version that is only a prefix of a heading" "has no non-empty '## 2.1' section" \
  2.1 v "$work/CHANGELOG.md" "$work/notes.md" v2.1
expect_fail "a missing changelog" "changelog not found" \
  2.1.0 v "$work/missing.md" "$work/notes.md" v2.1.0

# The repository's own changelogs at their current versions.
root=$(cd "$here/../.." && pwd)
cpu_version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/src/DotNetDifferentialEvolution/DotNetDifferentialEvolution.csproj")
gpu_version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/src/DotNetDifferentialEvolution.GPU/DotNetDifferentialEvolution.GPU.csproj")
expect_pass "the CPU package's changelog has its version ($cpu_version)" \
  "$cpu_version" v "$root/CHANGELOG.md" "$work/notes.md" "v$cpu_version"
expect_pass "the GPU package's changelog has its version ($gpu_version)" \
  "$gpu_version" gpu-v "$root/src/DotNetDifferentialEvolution.GPU/CHANGELOG.md" "$work/notes.md" "gpu-v$gpu_version"

if [[ $failures -ne 0 ]]; then
  echo "$failures case(s) failed" >&2
  exit 1
fi
echo "all cases passed"
