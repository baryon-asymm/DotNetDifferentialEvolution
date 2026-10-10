#!/usr/bin/env python3
"""Proof of the decisions of coder_scope (ACCEPTANCE.md, H1).

Every test builds a temporary repository (which holds `.claude/scopes/`) and a temporary
worktree (which the scope file names), writes a scope granting node `src/A` and the writes
`src/A/[^/]+\\.cs`, and asks `decide` or `main` about one call of a `general-purpose` agent
under `--coder-types sonnet-coder,general-purpose`. Both directories are created and removed
by the tests.

    python -X utf8 tools/coder-scope/test_coder_scope.py
"""

from __future__ import annotations

import io
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from typing import Any, Dict, List, Optional

sys.path.insert(0, str(Path(__file__).resolve().parent))

import coder_scope as scope  # noqa: E402

CODER_TYPES = ("sonnet-coder", "general-purpose")
AGENT_ID = "a1b2c3d4e5f60718"
WRITE_A = r"src/A/[^/]+\.cs"


def real(path: str) -> str:
    """Return the real path of a temporary directory (short names and links resolved)."""
    return os.path.realpath(path)


def slashes(path: str) -> str:
    """Return path with forward slashes, the form a shell command quotes."""
    return path.replace("\\", "/")


class CoderScopeTest(unittest.TestCase):
    """Each test asks about one call; the scope is the one the module docstring describes."""

    def setUp(self) -> None:
        self.repo = self.directory()
        self.worktree = self.directory()
        self.elsewhere = self.directory()
        self.extra = self.directory()
        for relative in ("src/A/X.cs", "src/A/BOOT.md", "src/B/X.cs", "src/B/API.md",
                         "src/BOOT.md", "AGENTS.md", "X.sln"):
            self.touch(self.worktree, relative)
        self.touch(self.elsewhere, "secret.txt")
        self.extra_file = self.touch(self.extra, "prompt.md")
        self.publish()

    # ------------------------------------------------------------------ helpers

    def directory(self) -> str:
        holder = tempfile.TemporaryDirectory()
        self.addCleanup(holder.cleanup)
        return real(holder.name)

    @staticmethod
    def touch(root: str, relative: str) -> str:
        path = os.path.join(root, *relative.split("/"))
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as handle:
            handle.write("x\n")
        return path

    def scope_file(self, agent_id: str = AGENT_ID) -> str:
        return os.path.join(self.repo, ".claude", "scopes", "agent-" + agent_id + ".json")

    def publish(self, agent_id: str = AGENT_ID, **changes: Any) -> None:
        data: Dict[str, Any] = {
            "task": "implement the A node",
            "worktree": self.worktree,
            "nodes": ["src/A"],
            "write": [WRITE_A],
            "read": [self.extra_file],
        }
        data.update(changes)
        self.publish_text(json.dumps(data), agent_id)

    def publish_text(self, text: str, agent_id: str = AGENT_ID) -> None:
        path = self.scope_file(agent_id)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as handle:
            handle.write(text)

    def hook(self, tool: str, agent_type: Optional[str] = "general-purpose",
             agent_id: Optional[str] = AGENT_ID, cwd: Optional[str] = None, **tool_input: Any) -> Dict[str, Any]:
        hook: Dict[str, Any] = {"tool_name": tool, "tool_input": tool_input, "cwd": cwd or self.worktree}
        if agent_type is not None:
            hook["agent_type"] = agent_type
        if agent_id is not None:
            hook["agent_id"] = agent_id
        return hook

    def decide(self, hook: Dict[str, Any], coder_types=CODER_TYPES) -> Optional[str]:
        return scope.decide(hook, self.repo, coder_types)

    def at(self, relative: str) -> str:
        return os.path.join(self.worktree, *relative.split("/"))

    def shell(self, command: str, tool: str = "Bash", **hook_fields: Any) -> Optional[str]:
        return self.decide(self.hook(tool, command=command, **hook_fields))

    def assertAllowed(self, hook: Dict[str, Any]) -> None:
        self.assertIsNone(self.decide(hook))

    def assertRefused(self, reason: Optional[str], *fragments: str) -> None:
        self.assertIsNotNone(reason, "the call was allowed")
        assert reason is not None
        self.assertTrue(reason.startswith("coder-scope:"), reason)
        for fragment in fragments:
            self.assertIn(fragment, reason)

    # ------------------------------------------------------------------ allowed

    def test_reads_in_the_read_set_are_allowed(self) -> None:
        for relative in ("src/A/X.cs", "src/B/API.md", "src/BOOT.md", "AGENTS.md", "X.sln"):
            with self.subTest(relative=relative):
                self.assertAllowed(self.hook("Read", file_path=self.at(relative)))

    def test_an_extra_read_file_is_allowed(self) -> None:
        self.assertAllowed(self.hook("Read", file_path=self.extra_file))

    def test_an_extra_read_directory_is_allowed_and_searchable(self) -> None:
        self.publish(read=[self.extra])
        self.assertAllowed(self.hook("Read", file_path=self.extra_file))
        self.assertAllowed(self.hook("Grep", pattern="x", path=self.extra))

    def test_a_worktree_relative_extra_read_path_is_allowed(self) -> None:
        self.publish(read=["src/B"])
        self.assertAllowed(self.hook("Read", file_path=self.at("src/B/X.cs")))

    def test_write_and_edit_of_the_granted_pattern_are_allowed(self) -> None:
        for tool in ("Write", "Edit"):
            with self.subTest(tool=tool):
                self.assertAllowed(self.hook(tool, file_path=self.at("src/A/Y.cs")))

    def test_notebook_edit_is_judged_as_a_write(self) -> None:
        self.assertAllowed(self.hook("NotebookEdit", notebook_path=self.at("src/A/Y.cs")))
        self.assertRefused(self.decide(self.hook("NotebookEdit", notebook_path=self.at("src/B/Y.cs"))),
                           "write set")

    def test_glob_inside_the_worktree_is_allowed(self) -> None:
        self.assertAllowed(self.hook("Glob", pattern="src/**/*.cs", path=self.worktree))
        self.assertAllowed(self.hook("Glob", pattern="src/B/*.cs"))

    def test_grep_under_the_granted_node_is_allowed(self) -> None:
        self.assertAllowed(self.hook("Grep", pattern="x", path=self.at("src/A")))

    def test_the_build_command_in_the_worktree_is_allowed(self) -> None:
        command = 'cd "{}" && dotnet build X.sln'.format(slashes(self.worktree))
        self.assertIsNone(self.shell(command))
        self.assertIsNone(self.shell(command, tool="PowerShell"))

    def test_pathless_tools_are_allowed_without_any_scope_file(self) -> None:
        os.remove(self.scope_file())
        for tool in ("TodoWrite", "ToolSearch", "SendMessage", "SubagentHandback"):
            with self.subTest(tool=tool):
                self.assertAllowed(self.hook(tool))
        self.assertAllowed(self.hook("SubagentHandback", agent_id=None))

    def test_a_call_without_agent_type_is_allowed_unread(self) -> None:
        os.remove(self.scope_file())
        self.assertAllowed(self.hook("Read", agent_type=None, agent_id=None, file_path=self.at("src/B/X.cs")))

    def test_another_agent_type_is_allowed_unread(self) -> None:
        self.assertAllowed(self.hook("Read", agent_type="Explore", file_path=self.at("src/B/X.cs")))

    def test_the_default_coder_type_does_not_judge_general_purpose(self) -> None:
        hook = self.hook("Read", file_path=self.at("src/B/X.cs"))
        self.assertIsNone(self.decide(hook, scope.DEFAULT_CODER_TYPES))
        self.assertRefused(self.decide(self.hook("Read", agent_type="sonnet-coder",
                                                 file_path=self.at("src/B/X.cs")), scope.DEFAULT_CODER_TYPES),
                           "read set")

    def test_a_relative_path_resolves_against_cwd(self) -> None:
        self.assertAllowed(self.hook("Read", cwd=self.at("src/A"), file_path="X.cs"))
        self.assertRefused(self.decide(self.hook("Read", cwd=self.at("src/B"), file_path="X.cs")), "read set")

    def test_a_redirection_into_the_granted_pattern_is_allowed(self) -> None:
        self.assertIsNone(self.shell("echo x > src/A/Y.cs"))

    # ------------------------------------------------------------------ refused: the read set

    def test_read_of_a_neighbours_code_is_refused(self) -> None:
        reason = self.decide(self.hook("Read", file_path=self.at("src/B/X.cs")))
        self.assertRefused(reason, "read set", "src/B/X.cs", "implement the A node")

    def test_cat_and_sed_of_a_neighbours_code_are_refused(self) -> None:
        wt = slashes(self.worktree)
        for command in ('cd "{}" && cat src/B/X.cs'.format(wt), "sed -n 1,5p src/B/X.cs",
                        'cat "{}/src/B/X.cs"'.format(wt)):
            with self.subTest(command=command):
                self.assertRefused(self.shell(command), "read set", "heuristic")
                self.assertRefused(self.shell(command, tool="PowerShell"), "read set")

    def test_grep_under_a_neighbour_is_refused(self) -> None:
        self.assertRefused(self.decide(self.hook("Grep", pattern="x", path=self.at("src/B"))), "read set")

    def test_grep_without_a_path_from_the_worktree_root_is_refused(self) -> None:
        self.assertRefused(self.decide(self.hook("Grep", pattern="x")), "read set")

    def test_a_wildcard_read_of_a_neighbour_is_refused(self) -> None:
        self.assertRefused(self.shell("cat src/B/*.cs"), "read set")

    # ------------------------------------------------------------------ refused: the write set

    def test_write_of_a_neighbour_is_refused(self) -> None:
        self.assertRefused(self.decide(self.hook("Write", file_path=self.at("src/B/Y.cs"))), "write set")

    def test_write_of_a_boot_document_is_refused(self) -> None:
        self.assertRefused(self.decide(self.hook("Write", file_path=self.at("src/A/BOOT.md"))), "write set")
        self.assertRefused(self.decide(self.hook("Edit", file_path=self.at("AGENTS.md"))), "write set")

    def test_a_redirection_into_a_neighbour_is_refused(self) -> None:
        self.assertRefused(self.shell("echo x > src/B/Y.cs"), "write set")

    # ------------------------------------------------------------------ refused: outside the worktree

    def test_write_outside_the_worktree_is_refused(self) -> None:
        path = os.path.join(self.elsewhere, "Y.cs")
        self.assertRefused(self.decide(self.hook("Write", file_path=path)), "write outside the worktree")

    def test_read_outside_the_worktree_is_refused(self) -> None:
        path = os.path.join(self.elsewhere, "secret.txt")
        self.assertRefused(self.decide(self.hook("Read", file_path=path)), "read outside the worktree")

    def test_a_directory_change_out_of_the_worktree_is_refused(self) -> None:
        self.assertRefused(self.shell("cd {}".format(slashes(self.elsewhere))),
                           "directory change outside the worktree")
        self.assertRefused(self.shell("git -C {} status".format(slashes(self.elsewhere))),
                           "directory change outside the worktree")
        self.assertRefused(self.shell("Set-Location {}".format(slashes(self.elsewhere)), tool="PowerShell"),
                           "directory change outside the worktree")

    def test_glob_outside_the_worktree_is_refused(self) -> None:
        self.assertRefused(self.decide(self.hook("Glob", pattern="*.txt", path=self.elsewhere)),
                           "glob outside the worktree")

    def test_the_hook_cwd_outside_the_worktree_gives_the_coder_no_reach(self) -> None:
        self.assertRefused(self.decide(self.hook("Read", cwd=self.elsewhere, file_path="secret.txt")),
                           "read outside the worktree")

    def test_a_path_built_from_a_variable_is_refused(self) -> None:
        self.assertRefused(self.shell("cat $HOME/src/A/X.cs"), "variable")
        self.assertRefused(self.shell("cat %USERPROFILE%/src/A/X.cs"), "variable")

    # ------------------------------------------------------------------ refused: the scope itself

    def test_the_scope_file_is_not_readable_or_writable(self) -> None:
        self.assertRefused(self.decide(self.hook("Read", file_path=self.scope_file())), "scope files")
        self.assertRefused(self.decide(self.hook("Write", file_path=self.scope_file())), "scope files")
        self.assertRefused(self.shell('cat "{}"'.format(slashes(self.scope_file()))), "scope files")

    def test_the_scope_directory_stays_closed_even_as_an_extra_read_path(self) -> None:
        self.publish(read=[os.path.dirname(self.scope_file())])
        self.assertRefused(self.decide(self.hook("Read", file_path=self.scope_file())), "scope files")

    def test_a_tool_the_hook_does_not_know_is_refused(self) -> None:
        self.assertRefused(self.decide(self.hook("WebFetch", url="https://example.org")),
                           "tool not known to the hook", "WebFetch")

    def test_a_path_tool_without_a_path_is_refused(self) -> None:
        self.assertRefused(self.decide(self.hook("Read")), "names no path")

    def test_a_shell_call_without_command_text_is_refused(self) -> None:
        self.assertRefused(self.decide(self.hook("Bash")), "no command text")

    def test_a_hook_input_without_cwd_is_refused(self) -> None:
        hook = self.hook("Read", file_path=self.at("src/A/X.cs"))
        del hook["cwd"]
        self.assertRefused(self.decide(hook), "no cwd")

    # ------------------------------------------------------------------ refused: no usable scope

    def test_a_missing_scope_file_is_refused_as_not_yet_published(self) -> None:
        os.remove(self.scope_file())
        self.assertRefused(self.decide(self.hook("Read", file_path=self.at("src/A/X.cs"))),
                           "scope not yet published", "retry this call")

    def test_a_scope_is_found_by_agent_id_not_by_directory(self) -> None:
        self.publish("another0000", nodes=["src/B"], write=[r"src/B/[^/]+\.cs"])
        self.assertAllowed(self.hook("Read", agent_id="another0000", file_path=self.at("src/B/X.cs")))
        self.assertRefused(self.decide(self.hook("Read", agent_id=AGENT_ID, file_path=self.at("src/B/X.cs"))),
                           "read set")
        self.assertRefused(self.decide(self.hook("Read", agent_id="third000000", file_path=self.at("src/A/X.cs"))),
                           "scope not yet published")

    def test_a_malformed_scope_file_is_refused_as_unreadable(self) -> None:
        for text in ("{not json", "[]", '"text"', "{}", '{"nodes": "src/A", "write": [], "read": []}',
                     json.dumps({"nodes": ["src/A"], "write": [WRITE_A], "read": [], "worktree": self.worktree})
                     .replace('"read": []', '"read": [1]')):
            with self.subTest(text=text):
                self.publish_text(text)
                self.assertRefused(self.decide(self.hook("Read", file_path=self.at("src/A/X.cs"))),
                                   "scope file unreadable")

    def test_a_scope_without_worktree_is_refused_as_unreadable(self) -> None:
        self.publish_text(json.dumps({"nodes": ["src/A"], "write": [WRITE_A], "read": []}))
        self.assertRefused(self.decide(self.hook("Read", file_path=self.at("src/A/X.cs"))),
                           "scope file unreadable", "worktree")

    def test_a_worktree_that_is_gone_relative_or_not_a_directory_is_refused(self) -> None:
        for worktree in (os.path.join(self.elsewhere, "gone"), "src", self.at("src/A/X.cs"), None, 7):
            with self.subTest(worktree=worktree):
                self.publish(worktree=worktree)
                self.assertRefused(self.decide(self.hook("Read", file_path=self.at("src/A/X.cs"))),
                                   "scope file unreadable", "worktree")

    def test_a_wildcard_write_pattern_refuses_the_whole_scope(self) -> None:
        self.publish(write=[".*"])
        self.assertRefused(self.decide(self.hook("Write", file_path=self.at("src/A/Y.cs"))), "wildcard")
        self.assertRefused(self.decide(self.hook("Read", file_path=self.at("src/A/X.cs"))), "wildcard")

    def test_a_bad_write_pattern_is_refused_as_unreadable(self) -> None:
        self.publish(write=["src/A/("])
        self.assertRefused(self.decide(self.hook("Write", file_path=self.at("src/A/Y.cs"))),
                           "scope file unreadable")

    def test_a_root_or_escaping_node_refuses_the_whole_scope(self) -> None:
        for node in (".", "", "..", "src/../..", "/src/A", "C:/src"):
            with self.subTest(node=node):
                self.publish(nodes=[node])
                self.assertRefused(self.decide(self.hook("Read", file_path=self.at("src/A/X.cs"))),
                                   "scope file refused")

    def test_a_coder_without_agent_id_is_refused(self) -> None:
        self.assertRefused(self.decide(self.hook("Read", agent_id=None, file_path=self.at("src/A/X.cs"))),
                           "agent_id")

    def test_an_agent_id_that_would_leave_the_scope_directory_is_refused(self) -> None:
        for agent_id in ("../x", "a/b", "a\\b", "", ".", "..", 7):
            with self.subTest(agent_id=agent_id):
                self.assertRefused(self.decide(self.hook("Read", agent_id=agent_id,
                                                         file_path=self.at("src/A/X.cs"))), "agent_id")

    def test_an_internal_error_is_a_refusal_naming_it(self) -> None:
        original = scope.Scope.judge_tool

        def broken(*_: Any) -> None:
            raise RuntimeError("boom")

        scope.Scope.judge_tool = broken  # type: ignore[method-assign]
        try:
            reason = self.decide(self.hook("Read", file_path=self.at("src/A/X.cs")))
        finally:
            scope.Scope.judge_tool = original  # type: ignore[method-assign]
        self.assertRefused(reason, "internal error", "boom")

    # ------------------------------------------------------------------ main

    def run_main(self, argv: List[str], stdin: bytes):
        out, err = io.StringIO(), io.StringIO()
        code = scope.main(argv, stdin, out, err)
        return code, out.getvalue(), err.getvalue()

    def arguments(self) -> List[str]:
        return ["--repo", self.repo, "--coder-types", "sonnet-coder,general-purpose"]

    def test_main_prints_the_deny_json_and_exits_zero(self) -> None:
        hook = self.hook("Read", file_path=self.at("src/B/X.cs"))
        code, out, err = self.run_main(self.arguments(), json.dumps(hook).encode("utf-8"))
        self.assertEqual(0, code)
        self.assertEqual("", err)
        self.assertEqual(1, len(out.splitlines()))
        decision = json.loads(out)["hookSpecificOutput"]
        self.assertEqual("PreToolUse", decision["hookEventName"])
        self.assertEqual("deny", decision["permissionDecision"])
        self.assertTrue(decision["permissionDecisionReason"].startswith("coder-scope:"))
        self.assertIn("read set", decision["permissionDecisionReason"])

    def test_main_prints_nothing_to_allow(self) -> None:
        hook = self.hook("Read", file_path=self.at("src/A/X.cs"))
        self.assertEqual((0, "", ""), self.run_main(self.arguments(), json.dumps(hook).encode("utf-8")))

    def test_main_reads_the_default_coder_types(self) -> None:
        hook = self.hook("Read", file_path=self.at("src/B/X.cs"))
        self.assertEqual((0, "", ""), self.run_main(["--repo", self.repo], json.dumps(hook).encode("utf-8")))

    def test_main_exits_two_on_a_bad_argument(self) -> None:
        for argv in (["--nope"], ["--repo"], ["--repo", self.repo, "--coder-types"], ["stray"]):
            with self.subTest(argv=argv):
                code, out, err = self.run_main(argv, b"{}")
                self.assertEqual(2, code)
                self.assertEqual("", out)
                self.assertTrue(err.startswith("coder-scope:"), err)

    def test_main_exits_two_on_input_that_is_not_a_json_object(self) -> None:
        for stdin in (b"", b"not json", b"[1]", b'"text"', b"\xff\xfe"):
            with self.subTest(stdin=stdin):
                code, out, err = self.run_main(self.arguments(), stdin)
                self.assertEqual(2, code)
                self.assertEqual("", out)
                self.assertTrue(err.startswith("coder-scope:"), err)


if __name__ == "__main__":
    unittest.main()
