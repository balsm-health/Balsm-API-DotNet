using System.Globalization;
using System.Net;

namespace Balsm.Infrastructure.Auth;

/// <summary>What the person asked for when the code was requested.</summary>
public enum OtpEmailKind
{
    /// <summary>Signing in, or creating an account — the email does not say which.</summary>
    SignIn,

    /// <summary>Setting a new password for an existing account.</summary>
    ResetPassword,

    /// <summary>Confirming it is them before their account is deleted.</summary>
    DeleteAccount,
}

/// <summary>A rendered code email: subject line and HTML body.</summary>
public sealed record OtpEmailMessage(string Subject, string Html);

/// <summary>
/// Builds the code email from the branded layout in
/// <c>Templates/auth-otp/{ar,en}.html</c>.
/// </summary>
/// <remarks>
/// One layout per language and one set of words per <see cref="OtpEmailKind"/>,
/// so a new purpose is a new entry in <see cref="Copy"/> and nothing else. The
/// layout comes from the design system's "OTP login" email; only the words, the
/// chip colour and the button colour change between purposes.
/// </remarks>
public static class OtpEmail
{
    /// <summary>Content id the layout's logo <c>img</c> points at.</summary>
    public const string LogoContentId = "balsm-logo";

    private sealed record Words(
        string Subject,
        string Chip,
        string Title,
        string Preheader,
        string Intro,
        string Button,
        string LinkNote,
        string IgnoreNote);

    private sealed record Tone(string ChipBackground, string ChipText, string Button);

    // Colours from the design system's email templates: the sign-in blue, and
    // the amber chip and red action the security-alert email uses.
    private static readonly Tone Calm = new("#E4F0FF", "#08407A", "#1283FF");
    private static readonly Tone Danger = new("#FDF5DC", "#7A5A0F", "#D44A3C");

    private static Tone ToneFor(OtpEmailKind kind) => kind == OtpEmailKind.DeleteAccount ? Danger : Calm;

    // {0} is the number of minutes, already in the language's digits.
    private static readonly Dictionary<(OtpEmailKind, string), Words> Copy = new()
    {
        [(OtpEmailKind.SignIn, "en")] = new(
            Subject: "Your Balsm sign-in code",
            Chip: "SIGN-IN",
            Title: "Your sign-in code",
            Preheader: "Your sign-in code expires in {0} minutes and can only be used once.",
            Intro: "Enter this code to finish signing in to your Balsm.health account, or use the sign-in link below. Don't share it with anyone — not even Balsm staff.",
            Button: "Sign in with one tap",
            LinkNote: "The link works once, for {0} minutes.",
            IgnoreNote: "Didn't request this? You can safely ignore this email — no one can sign in without this code or link."),
        [(OtpEmailKind.SignIn, "ar")] = new(
            Subject: "رمز الدخول إلى بلسم",
            Chip: "تسجيل الدخول",
            Title: "رمز الدخول الخاص بك",
            Preheader: "رمز الدخول الخاص بك صالح لمدة {0} دقائق ولا يُستخدم إلا مرة واحدة.",
            Intro: "أدخل هذا الرمز لإتمام تسجيل الدخول إلى حسابك على بلسم، أو استخدم رابط الدخول أدناه. لا تشارك هذا الرمز مع أي شخص، بما في ذلك فريق بلسم.",
            Button: "سجّل الدخول بنقرة واحدة",
            LinkNote: "يعمل الرابط مرة واحدة، لمدة {0} دقائق.",
            IgnoreNote: "لم تطلب هذا الرمز؟ يمكنك تجاهل هذه الرسالة بأمان — لن يستطيع أحد تسجيل الدخول دون هذا الرمز أو الرابط."),

        [(OtpEmailKind.ResetPassword, "en")] = new(
            Subject: "Reset your Balsm password",
            Chip: "PASSWORD RESET",
            Title: "Reset your password",
            Preheader: "Your password reset code expires in {0} minutes and can only be used once.",
            Intro: "Enter this code to choose a new password for your Balsm.health account, or use the link below. Don't share it with anyone — not even Balsm staff.",
            Button: "Reset my password",
            LinkNote: "The link works once, for {0} minutes.",
            IgnoreNote: "Didn't ask to reset your password? You can safely ignore this email — your password stays as it is."),
        [(OtpEmailKind.ResetPassword, "ar")] = new(
            Subject: "إعادة تعيين كلمة المرور في بلسم",
            Chip: "إعادة تعيين كلمة المرور",
            Title: "إعادة تعيين كلمة المرور",
            Preheader: "رمز إعادة تعيين كلمة المرور صالح لمدة {0} دقائق ولا يُستخدم إلا مرة واحدة.",
            Intro: "أدخل هذا الرمز لاختيار كلمة مرور جديدة لحسابك على بلسم، أو استخدم الرابط أدناه. لا تشارك هذا الرمز مع أي شخص، بما في ذلك فريق بلسم.",
            Button: "إعادة تعيين كلمة المرور",
            LinkNote: "يعمل الرابط مرة واحدة، لمدة {0} دقائق.",
            IgnoreNote: "لم تطلب إعادة تعيين كلمة المرور؟ يمكنك تجاهل هذه الرسالة بأمان — ستبقى كلمة مرورك كما هي."),

        [(OtpEmailKind.DeleteAccount, "en")] = new(
            Subject: "Confirm deleting your Balsm account",
            Chip: "ACCOUNT DELETION",
            Title: "Confirm it's you",
            Preheader: "Your confirmation code expires in {0} minutes and can only be used once.",
            Intro: "Someone asked to delete your Balsm.health account. Enter this code to confirm it is you, or use the link below. Nothing is deleted until you confirm on the next screen. Don't share this code with anyone — not even Balsm staff.",
            Button: "Continue to delete my account",
            LinkNote: "The link works once, for {0} minutes.",
            IgnoreNote: "Didn't ask to delete your account? Ignore this email and your account stays as it is. If this keeps happening, change your password."),
        [(OtpEmailKind.DeleteAccount, "ar")] = new(
            Subject: "تأكيد حذف حسابك في بلسم",
            Chip: "حذف الحساب",
            Title: "تأكيد هويتك",
            Preheader: "رمز التأكيد صالح لمدة {0} دقائق ولا يُستخدم إلا مرة واحدة.",
            Intro: "طلب أحدهم حذف حسابك على بلسم. أدخل هذا الرمز لتأكيد أنك صاحب الطلب، أو استخدم الرابط أدناه. لن يُحذف أي شيء قبل أن تؤكد في الشاشة التالية. لا تشارك هذا الرمز مع أي شخص، بما في ذلك فريق بلسم.",
            Button: "المتابعة لحذف حسابي",
            LinkNote: "يعمل الرابط مرة واحدة، لمدة {0} دقائق.",
            IgnoreNote: "لم تطلب حذف حسابك؟ تجاهل هذه الرسالة وسيبقى حسابك كما هو. إذا تكرر ذلك، غيّر كلمة مرورك."),
    };

    private static readonly Dictionary<string, string> ExpiresLine = new()
    {
        ["en"] = "Expires in {0} minutes",
        ["ar"] = "تنتهي صلاحية الرمز خلال {0} دقائق",
    };

    /// <summary>
    /// The template language for a client's language tag: Arabic for any
    /// <c>ar</c> tag (<c>ar</c>, <c>ar-EG</c>, <c>ar_SA</c>), English otherwise.
    /// </summary>
    public static string LanguageFor(string? tag) =>
        tag is not null && tag.TrimStart().StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";

    /// <summary>Fills <paramref name="layout"/> — the language's template file — for one email.</summary>
    public static OtpEmailMessage Render(
        string layout, OtpEmailKind kind, string language, string code, string linkUrl, int expiryMinutes, int year)
    {
        var lang = LanguageFor(language);
        var words = Copy[(kind, lang)];
        var tone = ToneFor(kind);
        var minutes = Digits(expiryMinutes, lang);
        string Text(string s) => WebUtility.HtmlEncode(string.Format(CultureInfo.InvariantCulture, s, minutes));

        var html = layout
            .Replace("{{TITLE}}", Text(words.Title))
            .Replace("{{PREHEADER}}", Text(words.Preheader))
            .Replace("{{CHIP}}", Text(words.Chip))
            .Replace("{{INTRO}}", Text(words.Intro))
            .Replace("{{EXPIRES}}", Text(ExpiresLine[lang]))
            .Replace("{{BUTTON}}", Text(words.Button))
            .Replace("{{LINK_NOTE}}", Text(words.LinkNote))
            .Replace("{{IGNORE_NOTE}}", Text(words.IgnoreNote))
            .Replace("{{CHIP_BG}}", tone.ChipBackground)
            .Replace("{{CHIP_FG}}", tone.ChipText)
            .Replace("{{BUTTON_BG}}", tone.Button)
            .Replace("{{YEAR}}", Digits(year, lang))
            // Last, so nothing above can be mistaken for a placeholder inside them.
            .Replace("{{OTP}}", WebUtility.HtmlEncode(Spaced(code)))
            .Replace("{{LINK_URL}}", WebUtility.HtmlEncode(linkUrl));

        return new OtpEmailMessage(words.Subject, html);
    }

    /// <summary>What goes out when the layout file is missing: the code and the link, unstyled.</summary>
    public static OtpEmailMessage RenderPlain(OtpEmailKind kind, string language, string code, string linkUrl, int expiryMinutes)
    {
        var lang = LanguageFor(language);
        var words = Copy[(kind, lang)];
        var minutes = Digits(expiryMinutes, lang);
        var expires = string.Format(CultureInfo.InvariantCulture, ExpiresLine[lang], minutes);
        var html = $"<p>{WebUtility.HtmlEncode(words.Title)}: <strong>{WebUtility.HtmlEncode(Spaced(code))}</strong></p>"
                 + $"<p>{WebUtility.HtmlEncode(expires)}</p>"
                 + $"<p><a href=\"{WebUtility.HtmlEncode(linkUrl)}\">{WebUtility.HtmlEncode(words.Button)}</a></p>";
        return new OtpEmailMessage(words.Subject, html);
    }

    // "482913" reads as "482 913". The code itself stays in Latin digits in
    // both languages: it is typed into a field that expects them.
    private static string Spaced(string code) =>
        code.Length == 6 ? $"{code[..3]} {code[3..]}" : code;

    // Body text follows the language: Arabic-Indic digits in Arabic.
    private static string Digits(int value, string lang)
    {
        var s = value.ToString(CultureInfo.InvariantCulture);
        return lang == "ar" ? string.Concat(s.Select(c => char.IsAsciiDigit(c) ? (char)('٠' + (c - '0')) : c)) : s;
    }
}
