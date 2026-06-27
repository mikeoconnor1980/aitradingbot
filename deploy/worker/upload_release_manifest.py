import hashlib
import json
import os
import pathlib
import subprocess
import sys
import tempfile


def compute_sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def run_az(args: list[str], capture_output: bool = False) -> str:
    command = ["az", *args]
    result = subprocess.run(
        command,
        check=True,
        capture_output=capture_output,
        text=True,
    )
    return result.stdout.strip() if capture_output else ""


def add_entries(entries: list[dict[str, object]], section_name: str, section_entries: dict[str, dict], installer_dir: pathlib.Path, version_prefix: str) -> None:
    for key, entry in section_entries.items():
        filename = entry.get("filename", "")
        blob_name = entry.get("blobName", "")
        content_type = entry.get("contentType", "")
        size_bytes = int(entry.get("sizeBytes", 0))
        sha256 = entry.get("sha256", "").lower()

        if not filename or not blob_name or not content_type or not sha256:
            raise SystemExit(f"Manifest entry {section_name}.{key} is incomplete.")

        if not blob_name.startswith(version_prefix):
            raise SystemExit(
                f"Manifest entry {section_name}.{key} must use the versioned blob prefix {version_prefix}: {blob_name}")

        local_path = installer_dir / filename
        if not local_path.is_file():
            raise SystemExit(f"Local artifact missing for manifest entry {section_name}.{key}: {local_path}")

        actual_size = local_path.stat().st_size
        if actual_size != size_bytes:
            raise SystemExit(
                f"Manifest size mismatch for {section_name}.{key}. Manifest={size_bytes} actual={actual_size}")

        actual_sha = compute_sha256(local_path)
        if actual_sha != sha256:
            raise SystemExit(
                f"Manifest SHA256 mismatch for {section_name}.{key}. Manifest={sha256} actual={actual_sha}")

        entries.append({
            "section": section_name,
            "key": key,
            "filename": filename,
            "blob_name": blob_name,
            "content_type": content_type,
            "local_path": local_path,
            "size_bytes": actual_size,
            "sha256": actual_sha,
        })


def verify_remote_blob(storage_account: str, container_name: str, blob_name: str, expected_size: int, expected_sha: str) -> None:
    remote_size = int(run_az([
        "storage", "blob", "show",
        "--account-name", storage_account,
        "--container-name", container_name,
        "--name", blob_name,
        "--auth-mode", "login",
        "--query", "properties.contentLength",
        "--output", "tsv",
    ], capture_output=True))

    if remote_size != expected_size:
        raise SystemExit(
            f"Remote size mismatch for {blob_name}. Remote={remote_size} expected={expected_size}")

    with tempfile.NamedTemporaryFile(delete=False) as temp_file:
        temp_path = pathlib.Path(temp_file.name)

    try:
        run_az([
            "storage", "blob", "download",
            "--account-name", storage_account,
            "--container-name", container_name,
            "--name", blob_name,
            "--file", str(temp_path),
            "--overwrite", "true",
            "--auth-mode", "login",
        ])
        remote_sha = compute_sha256(temp_path)
    finally:
        temp_path.unlink(missing_ok=True)

    if remote_sha != expected_sha:
        raise SystemExit(
            f"Remote SHA256 mismatch for {blob_name}. Remote={remote_sha} expected={expected_sha}")


def main() -> int:
    storage_account = os.environ.get("STORAGE_ACCOUNT", "")
    container_name = os.environ.get("CONTAINER_NAME", "")
    installer_dir_value = os.environ.get("INSTALLER_DIR", "")
    manifest_path_value = os.environ.get("MANIFEST_PATH", "")

    if not storage_account or not container_name or not installer_dir_value or not manifest_path_value:
        raise SystemExit("STORAGE_ACCOUNT, CONTAINER_NAME, INSTALLER_DIR, and MANIFEST_PATH must all be set.")

    installer_dir = pathlib.Path(installer_dir_value)
    manifest_path = pathlib.Path(manifest_path_value)

    if not manifest_path.is_file():
        raise SystemExit(f"Release manifest not found: {manifest_path}")

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    version = manifest.get("version", "")
    if not version:
        raise SystemExit("Manifest version is missing.")

    version_prefix = f"v{version}/"
    files = manifest.get("files") or {}
    artifacts = manifest.get("artifacts") or {}

    if "exe" not in files or "zip" not in files:
        raise SystemExit("Manifest must include exe and zip file entries.")

    entries: list[dict[str, object]] = []
    add_entries(entries, "files", files, installer_dir, version_prefix)
    add_entries(entries, "artifacts", artifacts, installer_dir, version_prefix)

    for entry in entries:
        print(f"Uploading {entry['blob_name']}...")
        run_az([
            "storage", "blob", "upload",
            "--account-name", storage_account,
            "--container-name", container_name,
            "--file", str(entry["local_path"]),
            "--name", str(entry["blob_name"]),
            "--content-type", str(entry["content_type"]),
            "--overwrite", "true",
            "--auth-mode", "login",
        ])

    print("Uploading latest.json...")
    run_az([
        "storage", "blob", "upload",
        "--account-name", storage_account,
        "--container-name", container_name,
        "--file", str(manifest_path),
        "--name", "latest.json",
        "--content-type", "application/json",
        "--overwrite", "true",
        "--auth-mode", "login",
    ])

    for entry in entries:
        verify_remote_blob(
            storage_account,
            container_name,
            str(entry["blob_name"]),
            int(entry["size_bytes"]),
            str(entry["sha256"]),
        )

    verify_remote_blob(
        storage_account,
        container_name,
        "latest.json",
        manifest_path.stat().st_size,
        compute_sha256(manifest_path),
    )

    print("Manifest and blob consistency checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())