"""Tests of history_guard.py: one scenario per rule, each in a throwaway git repository.

Run: python -X utf8 test_history_guard.py   (standard library and git only)
"""
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

GUARD = Path(__file__).with_name("history_guard.py")

PARENT_BOOT = """# BOOT.md - parent

## Purpose

The parent solves the thing.

## Constraints

Rule shared by both children.

Rule that binds only the child.

## Acceptance criteria

- [x] It works (2026-10-01, test_it).
"""

HISTORY = """# HISTORY.md - parent

<a id="old-figure"></a>
## 2026-09-01 - from "Constraints" - an old figure

> The figure was 7.
"""


def git(repo, *args):
    subprocess.run(["git", "-C", str(repo), *args], check=True, capture_output=True,
                   text=True, encoding="utf-8")


def write(repo, rel, text):
    path = Path(repo, rel)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(text.encode("utf-8"))


class Repo:
    """A repository with the parent node at base, and a branch `work` to change."""

    def __init__(self, tmp):
        self.path = Path(tmp)
        git(self.path, "init", "-q", "-b", "main")
        git(self.path, "config", "user.email", "t@example.com")
        git(self.path, "config", "user.name", "t")
        git(self.path, "config", "core.autocrlf", "false")
        write(self.path, "src/Parent/BOOT.md", PARENT_BOOT)
        write(self.path, "src/Parent/HISTORY.md", HISTORY)
        write(self.path, "src/Other/BOOT.md", "# BOOT.md - other\n")
        self.commit("base")
        git(self.path, "checkout", "-q", "-b", "work")

    def commit(self, message):
        git(self.path, "add", "-A")
        git(self.path, "commit", "-q", "-m", message)

    def base(self):
        out = subprocess.run(["git", "-C", str(self.path), "merge-base", "main", "work"],
                             check=True, capture_output=True, text=True)
        return out.stdout.strip()

    def guard(self, *nodes, base=None):
        result = subprocess.run(
            [sys.executable, "-X", "utf8", str(GUARD), str(self.path),
             base or self.base(), "work", *nodes],
            capture_output=True, text=True, encoding="utf-8")
        return result.returncode, result.stdout


class HistoryGuardTest(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.repo = Repo(self._tmp.name)

    def tearDown(self):
        self._tmp.cleanup()

    def move_rule_to_history(self):
        boot = PARENT_BOOT.replace(
            "Rule shared by both children.\n",
            "⚠ 2026-09-20: was shared, now moved → HISTORY.md#shared-rule\n")
        write(self.repo.path, "src/Parent/BOOT.md", boot)
        history = HISTORY.replace(
            "# HISTORY.md - parent\n",
            "# HISTORY.md - parent\n\n<a id=\"shared-rule\"></a>\n"
            "## 2026-10-02 - from \"Constraints\" - shared rule\n\n"
            "> Rule shared by both children.\n")
        write(self.repo.path, "src/Parent/HISTORY.md", history)

    def test_a_line_moved_to_history_with_a_pointer_is_green(self):
        self.move_rule_to_history()
        self.repo.commit("move")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 0, out)

    def test_a_line_removed_without_a_copy_is_lost(self):
        write(self.repo.path, "src/Parent/BOOT.md",
              PARENT_BOOT.replace("Rule shared by both children.\n", ""))
        self.repo.commit("drop")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 1)
        self.assertIn("lost line: Rule shared by both children.", out)

    def test_a_removed_line_that_starts_with_two_dashes_is_still_lost(self):
        write(self.repo.path, "src/Parent/BOOT.md",
              PARENT_BOOT.replace("Rule shared by both children.\n",
                                  "Rule shared by both children.\n\n-- a dashed rule.\n"))
        self.repo.commit("dashed rule at base")
        git(self.repo.path, "branch", "-f", "main", "HEAD")
        write(self.repo.path, "src/Parent/BOOT.md", PARENT_BOOT)
        self.repo.commit("drop the dashed rule")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 1, out)
        self.assertIn("lost line: -- a dashed rule.", out)

    def test_a_removed_history_separator_breaks_append_only(self):
        write(self.repo.path, "src/Parent/HISTORY.md", HISTORY + "\n---\n")
        self.repo.commit("separator at base")
        git(self.repo.path, "branch", "-f", "main", "HEAD")
        write(self.repo.path, "src/Parent/HISTORY.md", HISTORY)
        self.repo.commit("drop the separator")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 1, out)
        self.assertIn("line removed from append-only HISTORY.md: ---", out)

    def test_a_line_added_outside_a_pointer_paragraph_is_stray(self):
        write(self.repo.path, "src/Parent/BOOT.md",
              PARENT_BOOT + "\nA brand new rule nobody reviewed.\n")
        self.repo.commit("add")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 1)
        self.assertIn("line added outside a pointer paragraph: A brand new rule", out)

    def test_an_undefined_anchor_is_red(self):
        write(self.repo.path, "src/Parent/BOOT.md",
              PARENT_BOOT + "\n⚠ 2026-09-21: was x, now y → HISTORY.md#nowhere\n")
        self.repo.commit("cite")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 1)
        self.assertIn("unresolved anchor HISTORY.md#nowhere", out)

    def test_a_file_outside_the_nodes_is_out_of_scope(self):
        self.move_rule_to_history()
        write(self.repo.path, "src/Other/BOOT.md", "# BOOT.md - other, edited\n")
        self.repo.commit("move and stray edit")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 1)
        self.assertIn("out of scope: src/Other/BOOT.md", out)

    def test_a_rule_moved_into_a_childs_boot_is_green(self):
        boot = PARENT_BOOT.replace(
            "Rule that binds only the child.\n",
            "The child's own rule lives in [Child](Child/BOOT.md).\n")
        write(self.repo.path, "src/Parent/BOOT.md", boot)
        write(self.repo.path, "src/Parent/Child/BOOT.md",
              "# BOOT.md - child\n\n## Constraints\n\nRule that binds only the child.\n")
        self.repo.commit("move down")
        code, out = self.repo.guard("src/Parent", "src/Parent/Child")
        self.assertEqual(code, 0, out)

    def test_a_new_nodes_boot_is_not_stray(self):
        write(self.repo.path, "src/Parent/Child/BOOT.md",
              "# BOOT.md - child\n\n## Purpose\n\nEverything here is new.\n")
        self.repo.commit("new child")
        code, out = self.repo.guard("src/Parent", "src/Parent/Child")
        self.assertEqual(code, 0, out)

    def test_a_qualified_citation_is_left_to_the_linter(self):
        write(self.repo.path, "src/Parent/BOOT.md",
              PARENT_BOOT + "\nSee the sibling's record → ../Other/HISTORY.md#elsewhere\n")
        self.repo.commit("qualified")
        code, out = self.repo.guard("src/Parent")
        self.assertNotIn("GUARD: src/Parent: unresolved anchor", out)
        self.assertEqual(code, 0, out)

    def test_a_line_removed_from_history_is_red(self):
        write(self.repo.path, "src/Parent/HISTORY.md",
              HISTORY.replace("> The figure was 7.\n", ""))
        self.repo.commit("rewrite history")
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 1)
        self.assertIn("line removed from append-only HISTORY.md: > The figure was 7.", out)

    def test_the_moving_head_as_base_reports_other_nodes_falsely(self):
        self.move_rule_to_history()
        self.repo.commit("move")
        git(self.repo.path, "checkout", "-q", "main")
        write(self.repo.path, "src/Other/BOOT.md", "# BOOT.md - other, moved on main\n")
        self.repo.commit("someone else's node on main")
        git(self.repo.path, "checkout", "-q", "work")
        head = subprocess.run(["git", "-C", str(self.repo.path), "rev-parse", "main"],
                              check=True, capture_output=True, text=True).stdout.strip()
        code, out = self.repo.guard("src/Parent", base=head)
        self.assertEqual(code, 1)
        self.assertIn("out of scope: src/Other/BOOT.md", out)
        code, out = self.repo.guard("src/Parent")
        self.assertEqual(code, 0, out)


if __name__ == "__main__":
    os.environ.setdefault("GIT_CONFIG_NOSYSTEM", "1")
    unittest.main(verbosity=1)
