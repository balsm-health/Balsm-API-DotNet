using System.Text;

namespace Balsm.CareDirectory.Domain;

/// <summary>
/// Splits a directory listing that carries both scripts in one string into an
/// English/Arabic pair.
///
/// Overture supplies exactly one name per place, so a bilingual pair does not
/// exist upstream — but 14% of Egyptian listings (5,610 of 38,395) write both
/// names into that single field: "Arizona Hospital - مستشفي الأريزونا",
/// "صيدليات شفيق - Shafik pharmacies". Recovering those is the largest source of
/// real bilingual pairs available without a second dataset.
///
/// Measured against the full Egyptian extract: 5,597 of 5,610 bilingual names
/// split cleanly; the remainder fail a guard and stay whole, which is the safe
/// outcome.
/// </summary>
public static class BilingualName
{
    // Separators writers put between the two halves, plus whitespace.
    private static readonly char[] Separators =
        [' ', '\t', '-', '–', '—', '.', ',', ':', '|', '/', '\\', '(', ')', '[', ']', '{', '}'];

    // A split half shorter than this is more likely noise than a name.
    private const int MinHalfLength = 3;

    /// <summary>
    /// Splits <paramref name="name"/> at the first script transition. Returns false
    /// when the name is single-script, or when either half fails a sanity guard —
    /// in which case the caller keeps the name whole rather than storing a fragment.
    /// </summary>
    public static bool TrySplit(string? name, out string? english, out string? arabic)
    {
        english = null;
        arabic = null;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var leading = FirstScript(name);
        if (leading is null)
        {
            return false;
        }

        var transition = TransitionIndex(name, leading.Value);
        if (transition is null)
        {
            return false;
        }

        var head = name[..transition.Value].Trim(Separators);
        var tail = name[transition.Value..].Trim(Separators);

        if (head.Length < MinHalfLength || tail.Length < MinHalfLength)
        {
            return false;
        }

        // Each half must actually contain the script it is being filed under. This
        // rejects a mostly-Arabic name with one stray Latin word early in it, which
        // would otherwise split into a one-word "English" name and lose the rest.
        var (candidateEnglish, candidateArabic) = leading.Value == Script.Arabic
            ? (tail, head)
            : (head, tail);

        if (!Contains(candidateEnglish, Script.Latin) || !Contains(candidateArabic, Script.Arabic))
        {
            return false;
        }

        english = candidateEnglish;
        arabic = candidateArabic;
        return true;
    }

    /// <summary>Which script a single-script name should be filed under.</summary>
    public static bool IsArabic(string? value) => Contains(value, Script.Arabic);

    private enum Script
    {
        Latin,
        Arabic
    }

    private static Script? ScriptOf(char ch)
    {
        // Arabic block U+0600–U+06FF, covering Arabic-Indic digits and punctuation
        // alongside letters — the transition test only cares which side we are on.
        if (ch is >= '؀' and <= 'ۿ')
        {
            return Script.Arabic;
        }

        return char.IsAsciiLetter(ch) ? Script.Latin : null;
    }

    private static Script? FirstScript(string value)
    {
        foreach (var ch in value)
        {
            var script = ScriptOf(ch);
            if (script is not null)
            {
                return script;
            }
        }

        return null;
    }

    private static int? TransitionIndex(string value, Script leading)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var script = ScriptOf(value[i]);
            if (script is not null && script != leading)
            {
                return i;
            }
        }

        return null;
    }

    private static bool Contains(string? value, Script script)
    {
        if (value is null)
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (ScriptOf(ch) == script)
            {
                return true;
            }
        }

        return false;
    }
}
