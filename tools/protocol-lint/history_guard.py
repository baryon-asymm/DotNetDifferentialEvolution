"""Guard for moving document text without loss (AGENTS.md §15, a node split or condensed).

Usage: python history_guard.py <repo> <base-commit> <branch> <node> [<node> ...]

<base-commit> is the merge base of the branch with the integration branch
(`git merge-base <integration> <branch>`), never the moving head of the integration
branch: a diff against a moving head pulls other nodes' changes in as "out of scope".
<node> is a node path from the repository root (`.` for the root); pass every node of the
task - the parent and each child a rule moved into.

Rules, for every node passed:
  - lost: every non-blank line removed from its BOOT.md reappears verbatim among the lines
    gained by the BOOT.md, HISTORY.md or ACCEPTANCE.md of any node passed (leading spaces,
    blockquote markers and repeated whitespace ignored), so a rule moved down into the child
    that owns it is found;
  - stray: every line added to a BOOT.md that existed at base belongs to a paragraph that
    carries a pointer ("HISTORY.md#", "-> [ACCEPTANCE.md](ACCEPTANCE.md)" or a link to a
    "/BOOT.md"), is a heading, or came verbatim from another BOOT.md of the task; a BOOT.md
    that did not exist at base (a new child) is not checked for stray lines;
  - append-only: no non-blank line is removed from its HISTORY.md;
  - anchors: every bare `HISTORY.md#<anchor>` cited in its BOOT.md or ACCEPTANCE.md is
    defined (an `<a id="...">` or a heading slug) in the HISTORY.md of the node, an
    ancestor or the root; qualified citations (`../HISTORY.md#x`, `a/b/HISTORY.md#x`) are
    left to the protocol linter;
  - out of scope: any changed file other than the BOOT.md, HISTORY.md and ACCEPTANCE.md of
    the nodes passed. Code files moved by a node split land here by design: a human reads
    them.

The guard answers one question: was anything lost. Known weaknesses:
  - lines are compared as a set, so a short common line ("- None.", "---") "reappears"
    anywhere;
  - a pointer paragraph may be reworded freely - only a human checks that a condensed
    wording kept every condition;
  - a rule added on purpose during a move (e.g. one restored from the criteria) is reported
    as stray, exactly as expected: a human reads it, or it goes in a separate commit;
  - append-only is checked for the nodes passed only; a removal from another node's
    HISTORY.md shows up merely as "out of scope".

Exit code 0 when nothing fails; otherwise every failure is printed as "GUARD: ...".
Tests: test_history_guard.py beside this file (standard library and git only).
"""
import re
import subprocess
import sys


def git(repo, *args):
    return subprocess.run(["git", "-C", repo, *args], capture_output=True, text=True, encoding="utf-8", check=True).stdout


def norm(line):
    line = line.strip()
    while line.startswith(">"):
        line = line[1:].strip()
    return re.sub(r"\s+", " ", line)


def diff_lines(repo, base, branch, path):
    out = git(repo, "diff", "--unified=0", "--no-color", f"{base}..{branch}", "--", path)
    removed, added = [], []
    in_hunk = False  # file headers ("--- a/x", "+++ b/x") come only before the first hunk;
    for line in out.splitlines():  # a removed line "---" or "-- x" inside a hunk is content
        if line.startswith("@@"):
            in_hunk = True
            continue
        if not in_hunk:
            continue
        if line.startswith("-"):
            removed.append(line[1:])
        elif line.startswith("+"):
            added.append(line[1:])
    return removed, added


def show(repo, branch, path):
    try:
        return git(repo, "show", f"{branch}:{path}")
    except subprocess.CalledProcessError:
        return ""


def paragraphs(text):
    block = []
    for line in text.splitlines():
        if line.strip():
            block.append(line)
        elif block:
            yield block
            block = []
    if block:
        yield block


def main():
    repo, base, branch, *nodes = sys.argv[1:]
    nodes = [n.strip("/").replace("\\", "/") for n in nodes]
    prefix = lambda n: "" if n in (".", "") else n + "/"
    allowed = {prefix(n) + f for n in nodes for f in ("BOOT.md", "HISTORY.md", "ACCEPTANCE.md")}
    changed = [p for p in git(repo, "diff", "--name-only", f"{base}..{branch}").splitlines() if p]
    failures = [f"out of scope: {p}" for p in changed if p not in allowed]

    # A line removed from one node's BOOT.md may reappear in any allowed node's documents: a rule moved down
    # into the child that owns it lands in that child's BOOT.md.
    gained = set()
    for n in nodes:
        for other in ("HISTORY.md", "ACCEPTANCE.md", "BOOT.md"):
            _, more = diff_lines(repo, base, branch, prefix(n) + other)
            gained |= {norm(l) for l in more if l.strip()}
    removed_anywhere = set()
    for n in nodes:
        gone, _ = diff_lines(repo, base, branch, prefix(n) + "BOOT.md")
        removed_anywhere |= {norm(l) for l in gone if l.strip()}

    for node in nodes:
        boot = prefix(node) + "BOOT.md"
        removed, added = diff_lines(repo, base, branch, boot)
        lost = [l for l in removed if l.strip() and norm(l) not in gained]
        new_boot = show(repo, branch, boot)
        pointer_lines = set()
        for block in paragraphs(new_boot):
            if any("HISTORY.md#" in l or "ACCEPTANCE.md](ACCEPTANCE.md)" in l or "/BOOT.md" in l for l in block):
                pointer_lines |= {norm(l) for l in block}
        # Lines a rule moved down from the parent brings into this BOOT.md, verbatim, are not stray.
        pointer_lines |= removed_anywhere
        existed = bool(show(repo, base, boot).strip())
        stray = [] if not existed else [l for l in added if l.strip() and norm(l) not in pointer_lines and not l.lstrip().startswith("#")
                 and norm(l) not in {norm(r) for r in removed}]
        history = ""
        parts = node.split("/")
        for k in range(len(parts), 0, -1):
            if parts[:k] != ["."]:
                history += show(repo, branch, "/".join(parts[:k]) + "/HISTORY.md")
        history += show(repo, branch, "HISTORY.md")
        defined = set(re.findall(r'<a id="([^"]+)"></a>', history)) | {
            re.sub(r"[^a-z0-9 -]", "", h.lower()).replace(" ", "-") for h in re.findall(r"^#+ (.+)$", history, re.M)}
        cited = set(re.findall(r"(?<![/\w.])HISTORY\.md#([A-Za-z0-9_-]+)", new_boot + show(repo, branch, prefix(node) + "ACCEPTANCE.md")))
        missing = sorted(a for a in cited if a not in defined and a != "anchor")
        count = sum(1 for l in new_boot.splitlines() if l.strip())
        print(f"{node}: BOOT.md {count} non-blank lines; removed {len([l for l in removed if l.strip()])}, "
              f"lost {len(lost)}, stray additions {len(stray)}, unresolved anchors {len(missing)}")
        failures += [f"{node}: lost line: {l.strip()[:120]}" for l in lost]
        failures += [f"{node}: line added outside a pointer paragraph: {l.strip()[:120]}" for l in stray]
        failures += [f"{node}: unresolved anchor HISTORY.md#{a}" for a in missing]
        history_removed, _ = diff_lines(repo, base, branch, prefix(node) + "HISTORY.md")
        failures += [f"{node}: line removed from append-only HISTORY.md: {l.strip()[:120]}"
                     for l in history_removed if l.strip()]

    for f in failures:
        print("GUARD:", f)
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
