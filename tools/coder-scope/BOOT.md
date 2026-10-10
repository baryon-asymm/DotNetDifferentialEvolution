# BOOT.md — tools/coder-scope

## Purpose

A Claude Code `PreToolUse` hook that holds a coder subagent to its task: it may read what
AGENTS.md §3 lets a node read and write only the paths its task grants, and every other call
is refused with a reason the coder reads. Without it the rule was text in a prompt: on
2026-10-09 a coder read `Devices/*.cs` and `Bookkeeping/*.cs` through `cat` and `sed`
([the GPU package's HISTORY.md](../../src/DotNetDifferentialEvolution.GPU/HISTORY.md#audit-fixes-decided-2026-10-10)).
The owner chose the hook on 2026-10-10.

Ported from AerospacePropellantThermodynamics' `tools/coder-scope` (version 9fff37d9, carried
in the boot-api-protocol skill as `orchestration/examples/coder_scope.apt.py`), with one
change of design: **a coder's scope is found by its agent id, not by its working directory.**
This repository's orchestrating session runs from another directory and its coders work in
worktrees the orchestrator makes by hand (`C:\Projects\dde-wt\<name>`), so a coder's `cwd` is
not its worktree; the scope file names the worktree instead.

## Invariants

- **Only a coder is judged.** A call whose hook input has no `agent_type`, or one not in the
  coder types (`--coder-types`, default `sonnet-coder`), is allowed unread: the
  orchestrator, and agents of other types.
- **A coder's call is judged against its scope file**,
  `<repo>/.claude/scopes/agent-<agent_id>.json`, with the lists `nodes`, `write`, `read`, the
  string `worktree` (an absolute path to an existing directory) and an optional `task`.
  No file: refused "scope not yet published: … retry this call"; a malformed one: refused
  "scope file unreadable"; no `agent_id`: refused.
- **The read set is AGENTS.md §3's**: files under a granted node; any `API.md`; `AGENTS.md`
  and `CLAUDE.md` at the worktree root; the `BOOT.md` and the build files (`*.sln`,
  `Directory.Build.*`, `Directory.Packages.props`, `global.json`, `.editorconfig`) of a
  granted node's ancestors; and the scope's extra `read` paths (files or directories,
  absolute or worktree-relative). Directory searches (Grep) only under a granted node or an
  extra path; name listings (Glob) anywhere inside the worktree.
- **The write set is the task's**: a worktree-relative path must fullmatch one of the `write`
  regular expressions (case-insensitive on Windows). A pattern that would match `AGENTS.md`,
  `BOOT.md` or `.claude/settings.local.json` refuses the whole scope.
- **Outside the worktree nothing is reachable** but the extra read paths; the scope directory
  is never readable or writable.
- **Shell commands are judged by their text** (Bash and PowerShell; a heuristic): tokens that
  look like paths are checked as reads, redirection targets as writes; `cd`-like words and
  `git -C` move the directory the next tokens resolve against, starting from the hook's
  `cwd`, and may not leave the worktree; a path built from a variable is refused; here-doc
  bodies are not paths.
- **Tools without paths pass** (`TodoWrite`, `ToolSearch`, `SendMessage`,
  `SubagentHandback`) even when the scope is gone, so a coder can always report; a tool the
  hook does not know is refused.
- **Nothing unjudged is allowed**: any internal error is a refusal naming it.
- **The decision is printed, never the exit code**: a refusal is exit 0 with
  `hookSpecificOutput.permissionDecision = "deny"` and the reason; exit 2 only for a bad
  command line or input that is not a JSON object.

## Dependencies

None.

Outside the tree: Python 3 (standard library only); Claude Code's hook input (`agent_type`,
`agent_id`, `tool_name`, `tool_input`, `cwd`).

## Constraints

- **Registered by the owner, not by a session** (the skill's `lessons/orchestration.md`: an
  orchestrator writing its own hook is self-modification). The exact registration text is in
  [API.md](API.md).
- **A registered hook whose script is missing refuses every call of every session of that
  project** (`python <no file>` exits 2). The registration is removed before the checkout it
  points to moves to a branch without this node.
- The orchestrator writes a coder's scope file right after launching it, with `json.dump` or
  a file tool (a shell here-doc breaks `\\` in the patterns), and deletes it after the merge.
- Cost: one Python start per tool call of every agent of the project (APT measured about
  55 ms).

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- **No allow on doubt.** A call the hook cannot judge is refused.
- **No registration written by a session.**
- **No path-based shortcut for the orchestrator**: it is told apart by `agent_type` only.
