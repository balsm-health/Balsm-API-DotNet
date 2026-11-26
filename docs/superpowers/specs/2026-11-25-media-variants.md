# Media with variants (spec, draft for approval)

Replaces the single-purpose `illustration.image_url` / `image_url_dark` with one
media shape that every module can serve and the app can render the same way.

## Goals

- One contract for any image or video the API points at (pregnancy artwork now;
  care directory, education, family later).
- A media item can have several **variants** along named **axes**. Some axes the
  app decides from its own state; others the user switches on screen.
- Every variant can carry a **thumbnail**; a video always has one (its poster).
- The app never requests a file that does not exist, and never tells the CDN
  more than it must.

## Wire shape

```json
"media": {
  "kind": "image",                         // image only for now (video later)
  "alt": "Baby at week 12",                // localized; the API already serves one language
  "credit": "medicalgraphics",             // key of a credits table, optional
  "source_url": "https://www.medicalgraphics.de/...",  // where it was published, optional
  "axes": [
    { "id": "theme",  "source": "app" },
    { "id": "gender", "source": "user", "label": "Baby",
      "options": [ { "value": "girl", "label": "Girl" }, { "value": "boy", "label": "Boy" } ],
      "default": "girl" }
  ],
  "renditions": [
    { "when": { "theme": "light", "gender": "girl" },
      "url": "https://cdn.balsm.health/content/pregnancy/week-12/girl-light.jpg",
      "mime": "image/jpeg", "width": 679, "height": 480, "bytes": 30795,
      "duration_s": null,
      "thumbnail": { "url": ".../girl-light-thumb.jpg", "mime": "image/jpeg",
                     "width": 160, "height": 113, "bytes": 4100 } },
    { "when": {}, "url": ".../default.jpg", "mime": "image/jpeg", "width": 679, "height": 480, "bytes": 30100 }
  ]
}
```

### Axes

- `source: "app"` — the app fills the value from its own state. Known ids:
  `theme` (`light` | `dark`), `locale` (`en` | `ar`), `platform` (`ios` |
  `android` | `web`). An app axis the app does not know is treated as unset.
- `source: "user"` — shown as a switch (segmented control / chips) on the media,
  with the axis `label` and each option's `label` (localized by the API).
  `default` is used until the user picks. The choice is remembered **on the
  device only**, per axis id, so picking "Boy" once applies to every media item
  with a `gender` axis. It is never sent to the server.
- `locale` renditions are filtered by the API to the request's `lang`, so the app
  only receives the ones it can use (smaller payload).

### Choosing a rendition (app)

1. Context = app axis values + the user's remembered choices (or each user axis's
   `default`).
2. Keep renditions whose `when` is satisfied by the context; an axis absent from
   `when` matches anything.
3. Prefer the most specific (most axes in `when`); `when: {}` is the fallback.
4. Among equals, pick the smallest `width` ≥ slot width × device pixel ratio
   (resolution variants are just renditions with the same `when`).
5. Lists and cards show `thumbnail` when present; the detail view shows the full
   file. Video shows the thumbnail as its poster and plays on tap.
6. A file that fails to load hides the media (no empty frame), as today.

### Rules the API enforces at load (content file validation)

- Every rendition `url` and `thumbnail.url` is absolute https.
- Every key in `when` is a declared axis, and every value is one of that axis's
  options (app axes: the known value set).
- Each user axis's `default` is one of its options.
- At least one rendition matches the all-defaults context (so something always
  shows).
- `kind: video` ⇒ every rendition has a `thumbnail` and `duration_s`.
- Authoring may use a URL template (`.../{gender}-{theme}.jpg`) plus the axis
  values; the API expands it at load into explicit renditions and the publish
  workflow checks every expanded file exists in `tools/content-images/`.

## Privacy

Requesting only the chosen variant would tell the CDN the user's choice (for
pregnancy, the baby's sex) and, per week, how far along the patient is. The app
therefore prefetches, when a guide opens, the matching rendition for **every
week and every option of each user axis** (app axes need not be hidden). Files
are small; the cost is a few hundred KB once per session.

## Where it lives

- API: `MediaDto`, `MediaAxisDto`, `MediaOptionDto`, `MediaRenditionDto`,
  `MediaFileDto` in `Balsm.SharedKernel` (contract only, no logic); the
  validator in `Balsm.Infrastructure`; pregnancy weeks swap `illustration` for
  `media` (breaking for the field; content version bump, OpenAPI CHANGELOG
  entry, Insomnia example, C4 update).
- App: `Media` value object + rendition selector in `packages/core`; one
  `MediaView` widget (thumbnail → full, video poster, user-axis switch, hide on
  error); remembered axis choices in the global KV store; the pregnancy guide
  uses `MediaView`.
- CDN: unchanged (`cdn.balsm.health/content/...`, Content images workflow).

## Decisions (2026-11-25)

1. Pregnancy uses only the `theme` axis now (light/dark files exist). No `gender`.
2. User axes are supported by the contract and rendered as a switch **on the
   media**, remembered per axis id on the device. No content uses one yet.
3. No video in the app now: `kind` is always `image`; video fields
   (`duration_s`, mandatory poster) are not implemented. Thumbnails are
   supported (optional) on image renditions.
4. Pregnancy weeks replace `baby.illustration` with `baby.media` (credit and
   source_url move into it). Content version bump; the app's storage key bumps.
