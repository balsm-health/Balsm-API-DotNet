namespace Balsm.SharedKernel.Media;

/// <summary>A dimension a media item's renditions vary along.</summary>
/// <param name="Id">
/// Axis name used as a key in each rendition's <c>when</c>. App axes: <c>theme</c>
/// (<c>light</c> | <c>dark</c>), <c>locale</c> (<c>en</c> | <c>ar</c>), <c>platform</c>
/// (<c>ios</c> | <c>android</c> | <c>web</c>).
/// </param>
/// <param name="Source">
/// Who sets the value: <c>app</c> (from its own state, never shown) or <c>user</c> (a switch on the
/// media; the choice is remembered on the device per axis id and never sent to the server).
/// </param>
/// <param name="Label">The switch's label, in the response's language; user axes only, null for app axes.</param>
/// <param name="Options">The values the user can pick, in display order; user axes only, null for app axes.</param>
/// <param name="Default">The option used until the user picks one; user axes only, null for app axes.</param>
public sealed record MediaAxisDto(
    string Id,
    string Source,
    string? Label = null,
    IReadOnlyList<MediaOptionDto>? Options = null,
    string? Default = null);
