using Balsm.SharedKernel.Media;

namespace Balsm.Infrastructure.Media;

/// <summary>
/// The rules every <see cref="MediaDto"/> in a content file must meet before it is served, so the
/// app never meets a variant it cannot choose, a file it cannot load over https, or a media item
/// with nothing to show.
/// </summary>
public static class MediaValidator
{
    /// <summary>The only kind served today.</summary>
    public const string Image = "image";

    /// <summary>An axis whose value the app fills from its own state.</summary>
    public const string AppSource = "app";

    /// <summary>An axis the user switches on the media.</summary>
    public const string UserSource = "user";

    /// <summary>Every app axis and the values it can take.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> AppAxes = new Dictionary<string, IReadOnlyList<string>>
    {
        ["theme"] = ["light", "dark"],
        ["locale"] = ["en", "ar"],
        ["platform"] = ["ios", "android", "web"],
    };

    /// <summary>
    /// Everything wrong with <paramref name="media"/>, empty when it can be served. The all-defaults
    /// context (<c>theme</c> light, <c>locale</c> each of <paramref name="locales"/>, <c>platform</c>
    /// unset, each user axis at its default) must match at least one rendition.
    /// </summary>
    public static IReadOnlyList<string> Validate(MediaDto media, IReadOnlyCollection<string> locales)
    {
        var problems = new List<string>();
        if (media.Kind != Image)
            problems.Add($"kind '{media.Kind}' is not '{Image}'");

        var allowed = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var axis in media.Axes)
        {
            if (!allowed.TryAdd(axis.Id, AllowedValues(axis, problems)))
                problems.Add($"axis '{axis.Id}' is declared twice");
        }

        if (media.Renditions.Count == 0)
            problems.Add("no renditions");

        foreach (var rendition in media.Renditions)
        {
            RequireHttps(rendition.Url, problems);
            if (rendition.Thumbnail is { } thumbnail)
                RequireHttps(thumbnail.Url, problems);

            foreach (var (id, value) in rendition.When)
            {
                if (!allowed.TryGetValue(id, out var values))
                    problems.Add($"rendition {rendition.Url} names undeclared axis '{id}'");
                else if (!values.Contains(value))
                    problems.Add($"rendition {rendition.Url} gives axis '{id}' the value '{value}', not one of {string.Join("|", values)}");
            }
        }

        foreach (var locale in locales)
        {
            var context = DefaultContext(media.Axes, locale);
            if (media.Renditions.Count > 0 && !media.Renditions.Any(r => r.When.All(w => context.TryGetValue(w.Key, out var v) && v == w.Value)))
                problems.Add($"no rendition matches the default context for locale '{locale}'");
        }

        return problems;
    }

    private static IReadOnlyList<string> AllowedValues(MediaAxisDto axis, List<string> problems)
    {
        switch (axis.Source)
        {
            case AppSource:
                if (AppAxes.TryGetValue(axis.Id, out var known))
                    return known;
                problems.Add($"app axis '{axis.Id}' is not one of {string.Join("|", AppAxes.Keys)}");
                return [];
            case UserSource:
                var options = axis.Options?.Select(o => o.Value).ToList() ?? [];
                if (options.Count == 0)
                    problems.Add($"user axis '{axis.Id}' has no options");
                if (axis.Default is null || !options.Contains(axis.Default))
                    problems.Add($"user axis '{axis.Id}' default '{axis.Default}' is not one of its options");
                return options;
            default:
                problems.Add($"axis '{axis.Id}' source '{axis.Source}' is not '{AppSource}' or '{UserSource}'");
                return [];
        }
    }

    private static Dictionary<string, string> DefaultContext(IEnumerable<MediaAxisDto> axes, string locale)
    {
        var context = new Dictionary<string, string> { ["theme"] = "light", ["locale"] = locale };
        foreach (var axis in axes.Where(a => a is { Source: UserSource, Default: not null }))
            context[axis.Id] = axis.Default!;
        return context;
    }

    private static void RequireHttps(string url, List<string> problems)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            problems.Add($"{url} is not an absolute https URL");
    }
}
