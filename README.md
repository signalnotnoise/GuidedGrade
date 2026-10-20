# GuidedGrade

Windows desktop assignment review and grading workspace. Open `GuidedGrade.slnx`
with a .NET 10 SDK and Windows desktop tooling. Tests:

```powershell
dotnet test GuidedGrade.Tests/GuidedGrade.Tests.csproj -c Release
```

## Repository maps

- [Source-reviewed architecture and behavior](KNOWLEDGE_GRAPH.md)
- [Searchable offline file/project graph](docs/knowledge-graph/graph.html)
- [Canonical graph JSON](docs/knowledge-graph/graph.json)
- [Graph setup, regeneration and limits](docs/knowledge-graph/README.md)

The offline Three.js viewer supports zoom, pan, area filters and relationship
navigation, with a node selector and inspector available without WebGL.

Python 3 and Git are required for graph updates. Enable the pre-commit hook once
per clone with `git config core.hooksPath .githooks` (integrate with an existing
hook configuration instead of replacing it). It reads staged inputs and stages
only the generated JSON/HTML outputs. CI checks for stale outputs.

To preview unstaged work, run `python scripts/update-knowledge-graph.py --worktree`.
Validate that preview with the same command plus `--check`. Normal generation
without `--worktree` reads the Git index. Stage intended source/tooling changes
before committing; the hook regenerates the preview from those staged inputs.
