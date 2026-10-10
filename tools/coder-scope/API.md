# API.md — tools/coder-scope

## Command line ⏳

```
python -X utf8 tools/coder-scope/coder_scope.py [--repo <dir>] [--coder-types <type>[,<type>...]]
```

- Reads one hook input (JSON) on standard input; prints nothing to allow, or one line
  `{"hookSpecificOutput": {"hookEventName": "PreToolUse", "permissionDecision": "deny",
  "permissionDecisionReason": "<reason>"}}` to refuse; exit 0. Exit 2 with a message on
  standard error for an unknown argument or input that is not a JSON object.
- `--repo` is the repository whose `.claude/scopes/` holds the scope files (default: two
  levels above the script). `--coder-types` lists the `agent_type` values judged (default
  `sonnet-coder`).
- Every refusal reason starts with `coder-scope:`.

## Scope file ⏳

`<repo>/.claude/scopes/agent-<agent_id>.json` (ignored by git):

```json
{
  "task": "one line the refusals quote",
  "worktree": "C:/Projects/dde-wt/<name>",
  "nodes": ["src/DotNetDifferentialEvolution.GPU/Devices"],
  "write": ["src/DotNetDifferentialEvolution\\.GPU/Devices/[^/]+\\.cs"],
  "read": ["C:/Users/<user>/AppData/Local/Temp/claude/<session>/scratchpad/<prompt>.md", "tools/protocol-lint"]
}
```

`nodes` are worktree-relative directories (not the root, no `..`); `write` are regular
expressions over worktree-relative paths with `/`; `read` are extra files or directories.

## Registration (the owner's) ⏳

In the settings of the project the orchestrating session runs from. For the session of
2026-10-10 that is `C:\Projects\DotNetDifferentialEvolution.GPU\.claude\settings.local.json`:

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "*",
        "hooks": [
          {
            "type": "command",
            "command": "python -X utf8 C:/Projects/DotNetDifferentialEvolution/tools/coder-scope/coder_scope.py --repo C:/Projects/DotNetDifferentialEvolution --coder-types sonnet-coder,general-purpose"
          }
        ]
      }
    ]
  }
}
```

`general-purpose` is the bridge until the `sonnet-coder` agent type is picked up by the
session; with it, every general-purpose agent of that project needs a scope file.

## Python module ⏳

`coder_scope.decide(hook: dict, repo: str, coder_types: tuple) -> str | None` returns the
refusal reason or `None`; `coder_scope.main(argv, stdin_bytes, stdout, stderr) -> int`. The
self-test is `python -X utf8 tools/coder-scope/test_coder_scope.py` (standard `unittest`),
run in CI beside the lint's.
