# Project instructions for Claude Code

@AGENTS.md
@BOOT.md

Follow the start procedure in the imported protocol (§10). Run the project lint
from the directory containing the imported BOOT.md. Use the same source roots
and exclusions before and after the change.

```sh
python -X utf8 tools/protocol-lint/protocol_lint.py . --exclude templates --strict
```
