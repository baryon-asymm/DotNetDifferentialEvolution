# ACCEPTANCE.md — tools/coder-scope

The node's acceptance criteria (AGENTS.md 3.2, §6), frozen 2026-10-10 before code. Each is
proven twice: green on a known answer and red once on the named mutation, applied to a
scratch copy and never committed.

## Checks H1–H3

- [ ] **H1, the decisions** (CI: `test_coder_scope.py`, temporary directories as repository
      and worktree). For a call of `agent_type` `general-purpose` under
      `--coder-types sonnet-coder,general-purpose`, with a scope granting node `src/A` and
      writes `src/A/[^/]+\.cs`: allowed — Read of `src/A/X.cs`, of `src/B/API.md`, of
      `src/BOOT.md`, of the root `AGENTS.md`, of an extra read file; Write and Edit of
      `src/A/Y.cs`; Glob inside the worktree; Grep under `src/A`; Bash `cd <worktree> &&
      dotnet build X.sln`; `TodoWrite` and `SubagentHandback` with no scope file at all.
      Refused, each with its reason — Read of `src/B/X.cs` ("read set"); Bash
      `cd <worktree> && cat src/B/X.cs` and `sed -n 1,5p src/B/X.cs`; Grep under `src/B`;
      Write of `src/B/Y.cs` and of `src/A/BOOT.md` ("write set"); a Write or Read outside
      the worktree; Bash `cd C:/elsewhere`; a path built from `$HOME`; a Read of the scope
      file; a tool the hook does not know; no scope file ("scope not yet published"); a
      malformed one ("unreadable"); a write pattern `.*` ("wildcard"); no `agent_id`.
      Allowed unread: a call without `agent_type`, and one of `agent_type` `Explore`.
      `main` prints the deny JSON and exits 0; a bad argument exits 2. Red: `decide`
      allowing a coder's call when its scope file is missing.
- [ ] **H2, the live probe** (by hand, after the owner's registration). A probe coder
      (general-purpose, Sonnet) with a scope on one node of a scratch worktree: a Read of a
      neighbour's `.cs`, `cat` of it through Bash and a Write outside its node are refused
      with the hook's reason, which the coder quotes; its own node, a neighbour's `API.md`
      and `AGENTS.md` are read and its node written. Red: the same probe before the
      registration reads the neighbour's `.cs` (as coder 2 did on 2026-10-09).
- [ ] **H3, the cost** (by hand): the median time of one hook call over 50 inputs on the
      owner's machine, recorded as a figure, not a threshold.
