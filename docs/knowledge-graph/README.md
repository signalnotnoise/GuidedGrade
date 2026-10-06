# GuidedGrade repository map

Open [graph.html](graph.html) in a browser. Search paths, filter by area, and
select nodes to inspect relationships. [graph.json](graph.json) is the data
source; the HTML includes a generated copy so it also works offline.

The viewer embeds the pinned Three.js 0.160.1 build (MIT) and license from
`scripts/knowledge-graph-vendor/`; no CDN, server or npm build is needed.
Scroll to zoom, drag to pan, or use arrow keys, +/- and Home in the graph.
Fit view resets the camera. Select a node on the graph or with the native selector
to highlight connections and navigate incoming/outgoing relationships in the inspector.
Directory edges are hidden by default; toggle them or limit the view to the selected
neighborhood. The inspector remains usable when WebGL is unavailable.
Vendored library files are excluded from graph nodes; renderer and vendor assets
are read from the index in commit mode and disk in preview mode.

The graph maps source files and runbooks. `contains` edges group files by
top-level directory. `references` edges come from relative ES module imports
and Markdown links. Declared XML project references/imports and solution project
entries also create reference edges. `declares-package` edges identify literal
NuGet package/version declarations. C# files are included as file nodes; their
calls, type references, dependency injection and runtime behavior are not parsed.
MSBuild conditions/properties, implicit compile items and transitive NuGet
resolution are not evaluated. See [the architecture map](../../KNOWLEDGE_GRAPH.md)
for manually source-reviewed runtime relationships.
It describes repository structure, not runtime traffic.
Add links between related runbooks to make their relationships visible.

Set up automatic updates once per clone (Python 3 and Git are required):

```sh
git config core.hooksPath .githooks
```

When first installing the tooling, stage `scripts/update-knowledge-graph.py`,
`scripts/knowledge-graph.html`, `scripts/test-knowledge-graph.py` and
`scripts/knowledge-graph-vendor/` and `.githooks/pre-commit` together. Index mode requires the renderer template and vendor assets to
be staged; if it is missing, the generator stops with setup guidance before
writing outputs. Use `--worktree` for a preview before staging.

The pre-commit hook reads the Git index, regenerates both outputs, and stages
them. Stage your intended changes before committing. Unstaged edits are not
included. A generation failure stops the commit. CI checks that committed
outputs match committed files, including when a hook was bypassed.

The renderer template is also read from the index in commit mode. File content
hashes (normalized LF) make text-only changes stale. Generated artifacts exclude
themselves; only supported source/document/build file types are enumerated.
No source text, student data or local settings contents are embedded. Worktree
preview includes non-ignored untracked supported files; inspect `.gitignore`
before placing private source or documents in this checkout.

Validation: `python scripts/test-knowledge-graph.py` uses a disposable Git fixture
to test staged/unstaged isolation, deterministic output, references, stale checks
and the real hook. It requires Git's `sh` (Git for Windows includes it).

To refresh manually after staging changes:

```sh
python scripts/update-knowledge-graph.py
```

To preview changes before staging them:

```sh
python scripts/update-knowledge-graph.py --worktree
```

Edit the renderer in [scripts/knowledge-graph.html](../../scripts/knowledge-graph.html)
and the extractor in [scripts/update-knowledge-graph.py](../../scripts/update-knowledge-graph.py).
The generated outputs should not be edited directly.
