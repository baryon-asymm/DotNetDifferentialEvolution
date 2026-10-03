# CLAUDE.md

Loader. The repository is run by the document-tree protocol; the protocol and the tree
root are plugged in below and enter the context of every session.

@AGENTS.md
@BOOT.md

⚠ There are no subject-matter claims about the system here and there must be none
(`AGENTS.md`, §2). Everything about the system lives in the `BOOT.md` and `API.md` of
the nodes: a second source of truth diverges from the first sooner or later, and it
cannot be corrected from within the tree.

## Before any work

Start procedure: `AGENTS.md`, §10: the chain of `BOOT.md` from the task's node to the
root, the `API.md` of the neighbours from that node's `## Dependencies`, the choice of
mode (design or coding), the linter before and after the work.

## Commands

- Protocol lint: `python -X utf8 tools/protocol-lint/protocol_lint.py . --exclude templates`
  (`--exclude templates` keeps the kit's document templates from being read as nodes)
- Build: `dotnet build DotNetDifferentialEvolution.sln -c Release`
- Tests, fast set: `dotnet test DotNetDifferentialEvolution.sln -c Release --no-build --filter "Category=Unit"`
- Tests, CI set: `dotnet test DotNetDifferentialEvolution.sln -c Release --no-build --filter "Category!=Slow&Category!=Gpu"`
- Tests, GPU (needs an OpenCL device): `dotnet test tests/DotNetDifferentialEvolution.GPU.Test -c Release --no-build`
- Documentation references: `bash scripts/check-doc-references.sh`

## Repository

- Default branch `main`; work happens on feature branches.
- Push, tag, publish and merges into `main` only on the owner's word, each time.
