#!/usr/bin/env python3
"""Publish the content images in this folder to Cloudflare R2.

Every image under tools/content-images/ is uploaded to the same bucket the map
packs use, under `content/<path>`, so cdn.balsm.health serves e.g.
tools/content-images/pregnancy/week-12.jpg at
https://cdn.balsm.health/content/pregnancy/week-12.jpg — the URL the pregnancy
content file names in `image_url`.

An object whose sha256 metadata already matches the file is skipped, so a run
with nothing changed uploads nothing.

Environment (same token as tools/map-packs/publish.py):
    R2_ACCOUNT_ID        Cloudflare account id
    R2_ACCESS_KEY_ID     R2 API token key
    R2_SECRET_ACCESS_KEY R2 API token secret
    R2_BUCKET            bucket name

Usage:
    python3 tools/content-images/publish.py --dry-run     # no credentials needed
    python3 tools/content-images/publish.py
Requires: pip install boto3
"""
import argparse
import hashlib
import os
import pathlib
import sys

HERE = pathlib.Path(__file__).parent
TYPES = {".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".png": "image/png", ".webp": "image/webp"}
REQUIRED_ENV = ("R2_ACCOUNT_ID", "R2_ACCESS_KEY_ID", "R2_SECRET_ACCESS_KEY", "R2_BUCKET")
# The name of an image stays the same when its bytes change, so the CDN and the
# app may keep a copy for a day, not forever.
CACHE_CONTROL = "public, max-age=86400"


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
    ap.add_argument("--prefix", default="content", help="key prefix inside the bucket")
    ap.add_argument("--dry-run", action="store_true",
                    help="print what would be uploaded; touches nothing and needs no credentials")
    args = ap.parse_args()

    images = sorted(p for p in HERE.rglob("*") if p.suffix.lower() in TYPES)
    if not images:
        sys.exit(f"no images under {HERE}")

    if not args.dry_run:
        missing = [k for k in REQUIRED_ENV if not os.environ.get(k)]
        if missing:
            sys.exit(f"missing environment: {', '.join(missing)}")
    s3 = None if args.dry_run else client(os.environ["R2_ACCOUNT_ID"])
    bucket = os.environ.get("R2_BUCKET", "<bucket>")

    uploaded = 0
    for path in images:
        key = f"{args.prefix}/{path.relative_to(HERE).as_posix()}"
        body = path.read_bytes()
        sha = hashlib.sha256(body).hexdigest()

        if args.dry_run:
            print(f"  would upload {key}  ({len(body) >> 10} KB)")
            continue

        try:
            head = s3.head_object(Bucket=bucket, Key=key)
            if head.get("Metadata", {}).get("sha256") == sha:
                print(f"  unchanged  {key}")
                continue
        except s3.exceptions.ClientError as e:
            if e.response.get("Error", {}).get("Code") not in ("404", "NoSuchKey", "NotFound"):
                raise

        s3.put_object(
            Bucket=bucket,
            Key=key,
            Body=body,
            ContentType=TYPES[path.suffix.lower()],
            CacheControl=CACHE_CONTROL,
            Metadata={"sha256": sha},
        )
        uploaded += 1
        print(f"  uploaded   {key}")

    print(f"{'DRY RUN — ' if args.dry_run else ''}{len(images)} images, {uploaded} uploaded")


if __name__ == "__main__":
    main()
