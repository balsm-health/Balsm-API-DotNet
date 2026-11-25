# Pregnancy weekly guidance

Reference material for the week-by-week pregnancy guidance served by
`GET /api/v1/content/pregnancy-weeks?lang=en|ar`.

| File | What it is |
|---|---|
| [`src/Balsm.API/Content/pregnancy-weeks.v1.json`](../../../src/Balsm.API/Content/pregnancy-weeks.v1.json) | **Source of truth.** Bilingual content embedded in the API; validated at startup (`PregnancyWeeksContent`). |
| [`reference.md`](reference.md) | Readable rendering of that file — every week in English and Arabic with its symptoms, tests, danger signs and cited sources. Generated; for clinical review. |
| [`research-output.json`](research-output.json) | The original research output (version `who-egmohp-2026-11-24`): the content as researched from WHO, Egypt MOHP and other verified sources, before it was adapted into the content file. Kept unchanged as a record of what the research said and where it came from. |

## Changing the content

1. Edit `src/Balsm.API/Content/pregnancy-weeks.v1.json` and bump its `version`.
2. Regenerate the reference: `./scripts/render-pregnancy-reference.py`
   (`--check` fails when it is out of date).
3. Run `dotnet test tests/Balsm.API.Tests --filter "FullyQualifiedName~PregnancyWeeks"`.
4. Have a licensed obstetrician review `reference.md` before release.

General information only, never patient data. The app fetches a new copy when
the version changes; no app release is needed.
