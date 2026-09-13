#!/usr/bin/env python3
"""Publish built map packs to Cloudflare R2 and emit the manifest the API serves.

Separate from build_packs.py on purpose: building is local, deterministic and
safe to repeat; publishing needs credentials, costs money and is visible to
users. Different blast radius, different script.

R2 is S3-compatible, so this is plain boto3 against R2's endpoint. Egress from
R2 is free, which is the whole reason it was chosen — see the CDN decision in
the design doc.

Environment:
    R2_ACCOUNT_ID        Cloudflare account id
    R2_ACCESS_KEY_ID     R2 API token key
    R2_SECRET_ACCESS_KEY R2 API token secret
    R2_BUCKET            bucket name, e.g. balsm-map-packs
    PACKS_BASE_URL       public base, e.g. https://cdn.balsm.health/packs

Usage:
    python3 tools/map-packs/publish.py --dry-run     # no credentials needed
    python3 tools/map-packs/publish.py
Requires: pip install boto3
"""
import argparse
import json
import os
import pathlib
import sys

HERE = pathlib.Path(__file__).parent

# Pack filenames carry their version, so a given name's bytes never change.
# That makes them safe to cache permanently; `immutable` additionally stops
# browsers revalidating on reload.
CACHE_CONTROL = "public, max-age=31536000, immutable"

# There is no registered media type for PMTiles. octet-stream keeps every proxy
# from trying to transform the body, and the app does not content-negotiate.
CONTENT_TYPE = "application/octet-stream"

REQUIRED_ENV = ("R2_ACCOUNT_ID", "R2_ACCESS_KEY_ID", "R2_SECRET_ACCESS_KEY", "R2_BUCKET")


def client(account_id: str):
    """Lazily imported so --dry-run works on a machine without boto3."""
    try:
        import boto3
    except ImportError:
        sys.exit("boto3 not installed — pip install boto3 (or use --dry-run)")
    return boto3.client(
        "s3",
        endpoint_url=f"https://{account_id}.r2.cloudflarestorage.com",
        aws_access_key_id=os.environ["R2_ACCESS_KEY_ID"],
        aws_secret_access_key=os.environ["R2_SECRET_ACCESS_KEY"],
        region_name="auto",
    )


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--build", type=pathlib.Path, default=HERE / "build")
    ap.add_argument("--prefix", default="packs", help="key prefix inside the bucket")
    ap.add_argument("--dry-run", action="store_true",
                    help="print what would be uploaded; touches nothing and needs no credentials")
    ap.add_argument("--force", action="store_true",
                    help="re-upload packs already present. Versioned names mean a name that "
                         "exists already has the right bytes, so this is only for a corrupted object.")
    args = ap.parse_args()

    manifest_path = args.build / "manifest.json"
    if not manifest_path.exists():
        sys.exit(f"no {manifest_path} — run build_packs.py first")
    packs = json.loads(manifest_path.read_text())["packs"]

    base_url = os.environ.get("PACKS_BASE_URL", "https://cdn.example.invalid/packs")
    if args.dry_run:
        print(f"DRY RUN — {len(packs)} packs, "
              f"{sum(p['size_bytes'] for p in packs) >> 20} MB total")
    else:
        missing = [k for k in REQUIRED_ENV if not os.environ.get(k)]
        if missing:
            sys.exit(f"missing environment: {', '.join(missing)}")
        if "PACKS_BASE_URL" not in os.environ:
            sys.exit("PACKS_BASE_URL is required — the manifest's urls are built from it")

    s3 = None if args.dry_run else client(os.environ["R2_ACCOUNT_ID"])
    bucket = os.environ.get("R2_BUCKET", "<bucket>")

    published = []
    for pack in packs:
        local = args.build / "packs" / pack["file"]
        if not local.exists():
            sys.exit(f"missing {local} — manifest and build directory disagree")
        key = f"{args.prefix}/{pack['file']}"

        if args.dry_run:
            print(f"  would upload {key}  ({pack['size_bytes'] >> 20} MB)")
        else:
            exists = False
            if not args.force:
                try:
                    s3.head_object(Bucket=bucket, Key=key)
                    exists = True
                except Exception:
                    exists = False
            if exists:
                print(f"  {key}: already published")
            else:
                print(f"  uploading {key} ({pack['size_bytes'] >> 20} MB)…")
                s3.upload_file(
                    str(local), bucket, key,
                    ExtraArgs={
                        "ContentType": CONTENT_TYPE,
                        "CacheControl": CACHE_CONTROL,
                        # Travels with the object so a download can be verified
                        # even by something that never saw the manifest.
                        "Metadata": {"sha256": pack["sha256"]},
                    },
                )

        published.append({**{k: v for k, v in pack.items() if k != "file"},
                          "url": f"{base_url.rstrip('/')}/{pack['file']}"})

    out = args.build / "manifest.published.json"
    out.write_text(json.dumps({"packs": published}, ensure_ascii=False, indent=2))
    print(f"\n{'would write' if args.dry_run else 'wrote'} {out}"
          f" — serve this from GET /care/packs")


if __name__ == "__main__":
    main()
