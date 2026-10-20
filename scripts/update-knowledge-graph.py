"""Render a deterministic repository graph from Git's index (the next commit).

Only staged, tracked files are read, so secrets in local configuration and
unstaged work never become graph content. Uses Python's standard library.
"""
import argparse
import hashlib
import json
import re
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parents[1]
OUTPUTS = {"docs/knowledge-graph/graph.json", "docs/knowledge-graph/graph.html"}
ASSETS = ("scripts/knowledge-graph.html", "scripts/knowledge-graph-vendor/three.min.js", "scripts/knowledge-graph-vendor/THREE-LICENSE.txt")
EXTENSIONS = {".md", ".mdx", ".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs", ".cs", ".csproj", ".py", ".sh", ".go", ".rs", ".java", ".kt", ".rb", ".php", ".c", ".h", ".cpp", ".hpp", ".swift", ".vue", ".svelte", ".sql"}
EXTENSIONS.update({".slnx", ".props", ".targets", ".html", ".yml", ".yaml", ".ps1"})
SPECIAL_FILES = {".githooks/pre-commit", ".gitattributes", ".gitignore"}


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT)


def read_source(path, worktree):
    raw = (ROOT / path).read_bytes() if worktree else git("show", f":{path}")
    return raw.decode("utf-8", errors="replace").replace("\r\n", "\n")


def build_graph(worktree=False):
    args = ("ls-files", "--cached", "--others", "--exclude-standard", "-z") if worktree else ("ls-files", "--cached", "-z")
    paths = sorted({p for p in git(*args).decode().split("\0")
                    if p and p not in OUTPUTS and not p.startswith("scripts/knowledge-graph-vendor/") and (PurePosixPath(p).suffix in EXTENSIONS or p in SPECIAL_FILES)
                    and (not worktree or (ROOT / p).is_file())})
    files = set(paths)
    nodes = [{"id": p, "label": PurePosixPath(p).name,
              "group": p.split("/")[0] if "/" in p else "root"} for p in paths]
    edges = set()
    packages = {}
    for path in paths:
        source = read_source(path, worktree)
        nodes[paths.index(path)]["sha256"] = hashlib.sha256(source.encode()).hexdigest()
        # Links in runbooks and relative ES module imports describe dependencies.
        suffix = PurePosixPath(path).suffix
        refs = re.findall(r"\]\(([^)]+)\)", source) if suffix in {".md", ".mdx"} else []
        if suffix in {".js", ".jsx", ".ts", ".tsx", ".mjs", ".cjs"}:
            refs += re.findall(r'(?:from\s+|import\s*)[\'"]([^\'"]+)[\'"]', source)
        if suffix in {".csproj", ".slnx", ".props", ".targets"}:
            for element in ET.fromstring(source).iter():
                tag = element.tag.rsplit("}", 1)[-1]
                if tag in {"ProjectReference", "Import", "Project"}:
                    ref = element.get("Include") or element.get("Path") or element.get("Project")
                    if ref:
                        refs.append(ref.replace("\\", "/"))
                elif tag == "PackageReference" and element.get("Include"):
                    name = element.get("Include")
                    version = element.get("Version") or element.findtext("Version") or "unspecified"
                    key = f"package:{name}@{version}"
                    packages[key] = {"id": key, "label": f"{name} {version}", "group": "NuGet declarations"}
                    edges.add((path, key, "declares-package"))
        for ref in refs:
            ref = ref.split("#")[0].strip("<>")
            if not ref or ":" in ref or ref.startswith("/"):
                continue
            parts = list(PurePosixPath(path).parent.parts)
            for part in ref.split("/"):
                if part == "..":
                    if parts:
                        parts.pop()
                elif part and part != ".":
                    parts.append(part)
            target = "/".join(parts)
            # TypeScript imports use .js extensions for the emitted Node modules.
            if target not in files and target.endswith(".js"):
                target = target[:-3] + ".ts"
            if target in files and target != path:
                edges.add((path, target, "references"))
        # Directory grouping makes the repository topology visible even for
        # files that have no explicit imports or Markdown links.
        group = path.split("/")[0] if "/" in path else "root"
        edges.add(("group:" + group, path, "contains"))
    for group in sorted({node["group"] for node in nodes}):
        nodes.append({"id": "group:" + group, "label": group, "group": group})
    nodes.extend(packages[key] for key in sorted(packages))
    return {"schemaVersion": 1, "source": "GuidedGrade file/document map and declared project dependencies (not a C# call graph)",
            "nodes": nodes,
            "edges": [{"source": a, "target": b, "kind": k} for a, b, k in sorted(edges)]}


def render(graph, worktree=False):
    template = read_source("scripts/knowledge-graph.html", worktree)
    three = read_source(ASSETS[1], worktree)
    license_text = read_source(ASSETS[2], worktree)
    three = re.sub(r"</script", lambda _: r"<\/script", three, flags=re.IGNORECASE)
    # The embedded copy allows opening the HTML directly without an HTTP server.
    data = json.dumps(graph, ensure_ascii=True).replace("<", "\\u003c")
    return template.replace("__THREE_JS__", three).replace("__THREE_LICENSE__", license_text.replace("<", "\\u003c")).replace("__GRAPH_JSON__", data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="fail if committed outputs are stale")
    parser.add_argument("--worktree", action="store_true", help="preview current files, including unstaged changes")
    args = parser.parse_args()
    if not args.worktree:
        for asset in ASSETS:
            result = subprocess.run(
                ["git", "cat-file", "-e", f":{asset}"], cwd=ROOT, capture_output=True)
            if result.returncode:
                parser.error(
                f"{asset} is missing from the Git index. "
                "For a local preview, run: python scripts/update-knowledge-graph.py --worktree. "
                "Before committing, stage the graph tooling with: git add -- "
                "scripts/update-knowledge-graph.py scripts/knowledge-graph.html "
                "scripts/test-knowledge-graph.py scripts/knowledge-graph-vendor .githooks/pre-commit")
    graph = build_graph(args.worktree)
    outputs = {"docs/knowledge-graph/graph.json": json.dumps(graph, indent=2) + "\n",
               "docs/knowledge-graph/graph.html": render(graph, args.worktree)}
    for relative, content in outputs.items():
        path = ROOT / relative
        if args.check:
            if not path.exists() or path.read_text(encoding="utf-8") != content:
                raise SystemExit(f"{relative} is stale; run python scripts/update-knowledge-graph.py")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(content, encoding="utf-8", newline="\n")
    print(f"Knowledge graph: {len(graph['nodes'])} nodes, {len(graph['edges'])} edges")


if __name__ == "__main__":
    main()
