# Balsm API — documentation index

Start with the repo [README](../README.md) for setup/run/test; this tree holds
the deeper material.

## Architecture (C4)

Mermaid C4 diagrams, one directory per context — the rule is context +
container for every module, dynamics for every non-trivial flow, updated in the
same PR as the code they describe.

| Context | Files |
|---|---|
| Care Directory | [context](architecture/c4/care-directory/context.md) · [map-packs context](architecture/c4/care-directory/context-map-packs.md) · [component](architecture/c4/care-directory/component.md) · [import dynamics](architecture/c4/care-directory/dynamic-import.md) · [nightly export dynamics](architecture/c4/care-directory/dynamic-map-pack-export.md) |
| Emergency QR | [context](architecture/c4/emergency-qr/context.md) · [container](architecture/c4/emergency-qr/container.md) · [permanent-refresh dynamics](architecture/c4/emergency-qr/dynamic-permanent-refresh.md) |
| Caching & rate limiting | [component](architecture/c4/caching-rate-limit/component.md) · [OTP rate-limit dynamics](architecture/c4/caching-rate-limit/dynamic-otp-rate-limit.md) |

## API reference

- [`api/openapi/v1/`](api/openapi/v1/) — generated OpenAPI specs (per module +
  aggregate `all.json`). Regenerate with `scripts/generate-openapi.sh`; never
  hand-edit.
- [`api/insomnia/`](api/insomnia/) — one runnable collection per module
  (Insomnia v5 YAML). Binding rule: every endpoint/DTO change updates the
  owning collection in the same commit.

## Design history

- [`superpowers/specs/`](superpowers/specs/) — approved feature designs
- [`superpowers/plans/`](superpowers/plans/) — the implementation plans built
  from them

## Operations

- [backup-and-restore.md](backup-and-restore.md)
- [`ops/`](ops/) — hosting services + request runbooks
