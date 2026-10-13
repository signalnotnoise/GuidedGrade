"""Isolated graph/index/hook regression checks; never stages the user's checkout."""
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile

SOURCE = Path(__file__).resolve().parents[1]


def main():
    with tempfile.TemporaryDirectory(prefix="guidedgrade-graph-") as temporary:
        root = Path(temporary)
        environment = dict(os.environ)
        # The fixture must never inherit an alternate index/worktree from a caller.
        for key in ("GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE"):
            environment.pop(key, None)

        def run(*args, ok=True):
            result = subprocess.run(args, cwd=root, env=environment, capture_output=True, text=True)
            if ok and result.returncode:
                raise AssertionError(result.stdout + result.stderr)
            return result

        def write(path, content):
            target = root / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(content, encoding="utf-8", newline="\n")

        for name in ("scripts/update-knowledge-graph.py", "scripts/knowledge-graph.html", ".githooks/pre-commit", "scripts/knowledge-graph-vendor/three.min.js", "scripts/knowledge-graph-vendor/THREE-LICENSE.txt"):
            write(name, (SOURCE / name).read_text(encoding="utf-8"))
        run("git", "init", "-q")
        run("git", "config", "core.autocrlf", "false")
        generator = (sys.executable, "scripts/update-knowledge-graph.py")
        missing = run(*generator, ok=False)
        assert missing.returncode != 0
        assert "missing from the Git index" in missing.stderr
        assert "--worktree" in missing.stderr and "git add --" in missing.stderr
        assert "Traceback" not in missing.stderr
        assert not (root / "docs/knowledge-graph/graph.json").exists()
        run(*generator, "--worktree")
        run("git", "add", "scripts/knowledge-graph.html")
        missing_vendor = run(*generator, ok=False)
        assert "three.min.js is missing from the Git index" in missing_vendor.stderr
        assert "Traceback" not in missing_vendor.stderr
        write("A.md", "Original\n")
        write("B.md", "Target\n")
        write("C.md", "Unstaged target\n")
        write(".gitignore", "secret.md\n")
        write("secret.md", "Never include me\n")
        write("App/App.csproj", '<Project><ItemGroup><ProjectReference Include="../Other/Other.csproj"/><PackageReference Include="Example" Version="1.0"/></ItemGroup></Project>')
        write("Other/Other.csproj", "<Project/>")
        run("git", "add", ".")
        generator = (sys.executable, "scripts/update-knowledge-graph.py")
        run(*generator)
        run(*generator, "--check")
        write("A.md", "[staged](B.md)\n")
        run("git", "add", "A.md")
        assert run(*generator, "--check", ok=False).returncode != 0
        write("A.md", "[unstaged](C.md)\n")
        with (root / "scripts/knowledge-graph.html").open("a", encoding="utf-8") as output:
            output.write("UNSTAGED_TEMPLATE")
        with (root / "scripts/knowledge-graph-vendor/three.min.js").open("a", encoding="utf-8") as output:
            output.write("\n// UNSTAGED_VENDOR")
        write("untracked.md", "Preview only\n")
        run(*generator)
        before = [(root / path).read_bytes() for path in ("docs/knowledge-graph/graph.json", "docs/knowledge-graph/graph.html")]
        run(*generator)
        assert before == [(root / path).read_bytes() for path in ("docs/knowledge-graph/graph.json", "docs/knowledge-graph/graph.html")]
        graph = json.loads(before[0])
        html = before[1].decode()
        embedded = re.search(r'<script id="data" type="application/json">(.*?)</script>', html, re.S)
        assert embedded and json.loads(embedded[1]) == graph
        ids = {node["id"] for node in graph["nodes"]}
        assert all(e["source"] in ids and e["target"] in ids for e in graph["edges"])
        assert "secret.md" not in ids and "untracked.md" not in ids
        assert "UNSTAGED_TEMPLATE" not in html
        assert "UNSTAGED_VENDOR" not in html
        assert "__THREE_JS__" not in html and "__THREE_LICENSE__" not in html
        assert "Permission is hereby granted" in html
        assert not any(path.startswith("scripts/knowledge-graph-vendor/") for path in ids)
        references = {(e["source"], e["target"]) for e in graph["edges"]}
        assert ("A.md", "B.md") in references and ("A.md", "C.md") not in references
        assert ("App/App.csproj", "Other/Other.csproj") in references
        assert ("App/App.csproj", "package:Example@1.0") in references
        shell = shutil.which("sh")
        if not shell and os.name == "nt":
            shell = str(Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "Git/bin/sh.exe")
        assert shell and Path(shell).exists(), "Git's shell is required to validate the pre-commit hook"
        run(shell, ".githooks/pre-commit")
        staged = set(run("git", "diff", "--cached", "--name-only").stdout.splitlines())
        assert {"docs/knowledge-graph/graph.json", "docs/knowledge-graph/graph.html"} <= staged
        assert run("git", "show", ":A.md").stdout == "[staged](B.md)\n"
        run(*generator, "--check")
        run(*generator, "--worktree")
        run(*generator, "--worktree", "--check")
        assert "UNSTAGED_VENDOR" in (root / "docs/knowledge-graph/graph.html").read_text(encoding="utf-8")
        preview = json.loads((root / "docs/knowledge-graph/graph.json").read_text())
        assert "untracked.md" in {n["id"] for n in preview["nodes"]}
        assert "secret.md" not in {n["id"] for n in preview["nodes"]}
        write("A.md", "Changed text without a reference\n")
        assert run(*generator, "--worktree", "--check", ok=False).returncode != 0
    print("Passed: missing-template guidance, staged isolation, template isolation, references, endpoints, embedded JSON, determinism, stale detection, hook and preview")


if __name__ == "__main__":
    main()
