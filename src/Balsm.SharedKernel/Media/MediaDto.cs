namespace Balsm.SharedKernel.Media;

/// <summary>
/// An image the API points at, with every file it exists as. Variants differ along named
/// <c>axes</c>; the app picks the rendition whose <c>when</c> best matches its context.
/// </summary>
/// <param name="Kind">What the media is: <c>image</c> (the only kind served today).</param>
/// <param name="Alt">What the media shows, for screen readers, in the response's language.</param>
/// <param name="Credit">Key of the owning document's credits table naming who made it; null when none is needed.</param>
/// <param name="SourceUrl">Where the media was first published; null when it was made for Balsm.</param>
/// <param name="Axes">The axes the renditions vary along; empty when there is a single file.</param>
/// <param name="Renditions">Every file the media exists as. At least one matches the all-defaults context.</param>
public sealed record MediaDto(
    string Kind,
    string Alt,
    string? Credit,
    string? SourceUrl,
    IReadOnlyList<MediaAxisDto> Axes,
    IReadOnlyList<MediaRenditionDto> Renditions);
