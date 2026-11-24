# Pregnancy weeks: research prompt

A prompt for a research assistant with web search, to replace the placeholder copy in the Balsm API's week-by-week pregnancy guidance with content drawn from verified medical sources.

- **Content file:** `src/Balsm.API/Content/pregnancy-weeks.v1.json`
- **Served at:** `GET /api/v1/content/pregnancy-weeks` (`?lang=en|ar`, ETag-revalidated per language)
- **History:** this prompt produced the first real content (`who-egmohp-2026-11-24`), replacing the placeholder copy. The output shape below is the one it was run with; the content file has since gained `labels`, `topic_labels`, a `symptom_guide` and a per-week `baby` object, so map new research onto the current file rather than pasting it over.

## Before publishing

1. Have a licensed obstetrician review the output. The prompt's third output lists what to check.
2. Make sure the file lists weeks 4–40 once each, ascending, with a `version`. The API refuses it otherwise, and its strict parser also refuses extra fields.
3. Set `source` to the real attribution; it currently says "[Partner organization]".
4. Replace the JSON file with a new `version` and redeploy the API. No app release is needed.

## The prompt

````markdown
You are preparing week-by-week pregnancy guidance for a health app used mainly in Egypt. Research it from verified medical sources and return it as JSON in the exact shape below.

## Sources

Use only authoritative medical sources, in this order of preference:

1. **Egypt.** Ministry of Health and Population (MOHP) maternal and antenatal care guidance, and UNICEF Egypt or WHO Egypt country-office material.
2. **WHO.** The WHO recommendations on antenatal care for a positive pregnancy experience (2016) and later updates, plus WHO EMRO (Eastern Mediterranean) maternal health pages.
3. **Other national health services and professional bodies.** NHS (UK) "Your pregnancy and baby guide", CDC, ACOG, RCOG and NICE antenatal guidance.
4. **Reputable clinical references.** Mayo Clinic and Cleveland Clinic, to confirm fetal development facts only.

Rules for sources:

- Do not use blogs, forums, commercial baby sites (BabyCenter, What to Expect and similar), AI-generated pages or social media.
- Every factual claim needs at least one source from the list above.
- Where sources disagree, follow WHO and Egyptian MOHP practice and note the conflict.

## Content for each week, 4 to 40 (37 entries)

- **title:** a short headline of at most 6 words, about the baby's development or the mother this week.
- **body:** 2 to 3 short sentences, at most about 280 characters per language. It should cover:
  - one fetal development fact appropriate to the week, such as approximate size or a milestone;
  - one practical, evidence-based tip for the mother, such as a nutrient (folic acid, iron, calcium), an antenatal visit or test due around this week (follow the WHO 8-contact schedule and the Egyptian MOHP visit schedule), activity, rest or a common symptom;
  - where relevant, a short "contact your doctor if…" line naming the danger signs WHO lists: bleeding, severe headache or blurred vision, swelling of the face or hands, fever, reduced fetal movement after week 28, leaking fluid, severe abdominal pain.

Tone and safety:

- Calm, plain and reassuring. No fear language, no diagnoses, no medication doses.
- Never contradict "follow your doctor's plan".
- Describe a single pregnancy only.

## Language

- Write the English and Arabic texts separately. The Arabic must not be a literal translation of the English.
- Arabic is Modern Standard Arabic that Egyptian readers find natural, addressed to the pregnant woman in the feminine form (أنتِ، تناولي، استشيري طبيبتك).
- Use Western digits (12، 20) in both languages.
- Do not copy sentences from any source; paraphrase every text in your own words. Short facts such as sizes and weeks are fine.

## Output

Output 1: JSON only, exactly this shape. Field names are snake_case, the weeks run 4 to 40 in ascending order, each week appears once, and there are no extra fields.

```json
{
  "version": "<source-tag>-<YYYY-MM-DD>",
  "source": { "en": "<short attribution, e.g. Based on WHO and Egypt MOHP guidance>", "ar": "<same in Arabic>" },
  "disclaimer": { "en": "General information, not medical advice.", "ar": "معلومات عامة، وليست نصيحة طبية." },
  "multiples_note": { "en": "This content describes a single pregnancy; follow your doctor's plan for twins/multiples.", "ar": "هذا المحتوى يصف حملاً بجنين واحد؛ اتبعي خطة طبيبتك لحمل التوائم." },
  "weeks": [
    { "week": 4, "title": { "en": "…", "ar": "…" }, "body": { "en": "…", "ar": "…" } }
  ]
}
```

Output 2: a separate citations table that is not part of the JSON. For each week, list the source URLs and the claim each one supports, with the date you accessed it. Mark anything you could not verify from a source on the list as UNVERIFIED instead of guessing.

Output 3: a short list of points a licensed obstetrician should check before publishing, for example Egyptian visit timing and supplement advice.
````
