namespace Balsm.SharedKernel.Media;

/// <summary>One file a media item exists as, and the context it is for.</summary>
/// <param name="When">
/// Axis id to value this file is for, e.g. <c>{"theme": "dark"}</c>. An axis absent from it matches
/// any value; <c>{}</c> is the fallback. The most specific match wins; among equals, the smallest
/// <c>width</c> that fills the slot.
/// </param>
/// <param name="Url">Absolute https URL of the file.</param>
/// <param name="Mime">Media type, e.g. <c>image/jpeg</c>.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Bytes">File size in bytes.</param>
/// <param name="Thumbnail">A smaller copy for lists and cards; null when the file itself is small enough.</param>
public sealed record MediaRenditionDto(
    IReadOnlyDictionary<string, string> When,
    string Url,
    string Mime,
    int Width,
    int Height,
    long Bytes,
    MediaFileDto? Thumbnail = null);
