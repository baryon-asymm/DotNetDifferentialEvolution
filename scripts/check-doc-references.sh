#!/usr/bin/env bash
#
# Verifies that every repository path a markdown document links to still exists.
#
# docs/ALGORITHMS.md maps formulas onto the files that implement them. A rename that leaves those
# links pointing at nothing is silent — the document still renders, it just stops being true. This
# is the cheapest guard against that: it checks paths only. Type and member names named in prose
# still rely on review.
#
# Usage: scripts/check-doc-references.sh [file.md ...]   (defaults to every .md under docs/)

set -euo pipefail

if [ "$#" -gt 0 ]; then
    documents=("$@")
else
    mapfile -t documents < <(find docs -name '*.md' | sort)
fi

if [ "${#documents[@]}" -eq 0 ]; then
    echo "No documents to check."
    exit 0
fi

status=0
checked=0

for document in "${documents[@]}"; do
    if [ ! -f "$document" ]; then
        echo "::error::not a file: $document"
        status=1
        continue
    fi

    directory=$(dirname "$document")

    # Every markdown link target: the "(...)" of "[text](...)".
    while IFS= read -r target; do
        # Skip anchors, external links and empty targets — only repository paths are checked.
        case "$target" in
            '' | '#'* | http://* | https://* | mailto:*) continue ;;
        esac

        # Drop any "#fragment" so a deep link to a file is checked as the file.
        target=${target%%#*}
        [ -z "$target" ] && continue

        checked=$((checked + 1))
        if [ ! -e "$directory/$target" ]; then
            echo "::error file=$document::broken path reference: $target"
            status=1
        fi
    done < <(grep -oE '\]\([^)]*\)' "$document" | sed -E 's/^\]\(//; s/\)$//')
done

if [ "$status" -eq 0 ]; then
    echo "$checked repository path reference(s) resolve."
fi

exit "$status"
