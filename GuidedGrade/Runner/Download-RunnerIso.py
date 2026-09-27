"""Download and verify the official Windows 11 Enterprise 25H2 evaluation ISO."""
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
import hashlib
import shutil
import subprocess

URL = "https://software-static.download.prss.microsoft.com/dbazure/888969d5-f34g-4e03-ac9d-1f9786c66749/26200.6584.250915-1905.25h2_ge_release_svc_refresh_CLIENTENTERPRISEEVAL_OEMRET_x64FRE_en-us.iso"
SIZE = 7092807680
SHA256 = "a61adeab895ef5a4db436e0a7011c92a2ff17bb0357f58b13bbc4062e535e7b9"
target = Path.home() / "Downloads" / "Windows11EnterpriseEvaluation.iso"
parts = target.with_suffix(".parts")
parts.mkdir(exist_ok=True)
offset = target.stat().st_size if target.exists() else 0
if offset > SIZE:
    raise RuntimeError("Existing ISO is larger than the expected download; it was not changed.")
chunk_size = 64 * 1024 * 1024
ranges = [(start, min(start + chunk_size, SIZE) - 1) for start in range(offset, SIZE, chunk_size)]

def download(bounds):
    start, end = bounds
    path = parts / f"{start}-{end}.part"
    expected = end - start + 1
    if path.exists() and path.stat().st_size == expected:
        return path
    result = subprocess.run([
        "curl.exe", "--fail", "--silent", "--show-error", "--retry", "3",
        "--max-filesize", str(expected), "--range", f"{start}-{end}",
        "--output", str(path), "--write-out", "%{http_code}", URL
    ], capture_output=True, text=True, check=True)
    if result.stdout.strip() != "206" or path.stat().st_size != expected:
        raise RuntimeError(f"Server did not return the requested range {start}-{end}")
    return path

with ThreadPoolExecutor(max_workers=8) as pool:
    completed = 0
    for future in as_completed([pool.submit(download, bounds) for bounds in ranges]):
        future.result()
        completed += 1
        print(f"Downloaded {completed}/{len(ranges)} chunks", flush=True)

with target.open("ab") as output:
    for start, end in ranges:
        with (parts / f"{start}-{end}.part").open("rb") as source:
            shutil.copyfileobj(source, output, 1024 * 1024)

with target.open("rb") as source:
    actual = hashlib.file_digest(source, "sha256").hexdigest()
if actual != SHA256:
    raise RuntimeError(f"ISO checksum mismatch: {actual}. Do not use this ISO.")
print(f"VERIFIED: {target}\nSHA256: {actual}", flush=True)
# Retain chunk files until VM setup succeeds; cleanup is a separate explicit step.
