namespace Balsm.SharedKernel.Media;

/// <summary>One value a user axis can take.</summary>
/// <param name="Value">The value as written in a rendition's <c>when</c>.</param>
/// <param name="Label">What the switch shows for it, in the response's language.</param>
public sealed record MediaOptionDto(string Value, string Label);
