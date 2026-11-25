namespace Balsm.SharedKernel.Media;

/// <summary>A file on the CDN.</summary>
/// <param name="Url">Absolute https URL of the file.</param>
/// <param name="Mime">Media type, e.g. <c>image/jpeg</c>.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Bytes">File size in bytes.</param>
public sealed record MediaFileDto(string Url, string Mime, int Width, int Height, long Bytes);
