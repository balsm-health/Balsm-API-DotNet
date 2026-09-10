using System.Text;

namespace Balsm.CareDirectory.Domain;

/// <summary>
/// Folds the orthographic variation Arabic search has to survive.
///
/// Egyptian listings spell the same word several ways — أشعة and اشعة (hamza
/// versus bare alif) both occur freely in the directory, as do ة/ه and ى/ي — so
/// comparing raw strings silently drops a large share of matches: a user
/// searching one spelling never finds a facility stored under the other.
///
/// Both sides of every comparison go through here: the importer persists
/// normalised columns, and the search handler normalises the incoming query.
/// The function is idempotent, so normalising an already-normalised value is
/// safe.
/// </summary>
public static class ArabicText
{
    // Combining diacritics: fathatan (U+064B) through sukun (U+0652), plus the
    // superscript alif (U+0670). Written as escapes because they are invisible
    // in source and would otherwise be unreviewable.
    private const char DiacriticFirst = 'ً';
    private const char DiacriticLast = 'ْ';
    private const char SuperscriptAlif = 'ٰ';
    private const char Tatweel = 'ـ';

    public static string? Normalize(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var sb = new StringBuilder(value.Length);

        foreach (var ch in value)
        {
            switch (ch)
            {
                // Every alif form folds to the bare alif.
                case 'أ': // أ hamza above
                case 'إ': // إ hamza below
                case 'آ': // آ madda
                case 'ٱ': // ٱ wasla
                    sb.Append('ا'); // ا
                    break;

                case 'ة': // ة ta marbuta
                    sb.Append('ه'); // ه
                    break;

                case 'ى': // ى alif maqsura
                    sb.Append('ي'); // ي
                    break;

                case Tatweel: // ـ purely decorative elongation
                    break;

                default:
                    if ((ch >= DiacriticFirst && ch <= DiacriticLast) || ch == SuperscriptAlif)
                    {
                        break;
                    }

                    sb.Append(char.ToLowerInvariant(ch));
                    break;
            }
        }

        return sb.ToString();
    }
}
