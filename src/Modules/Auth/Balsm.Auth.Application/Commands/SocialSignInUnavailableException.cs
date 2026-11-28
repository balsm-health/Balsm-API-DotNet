namespace Balsm.Auth.Application.Commands;

/// <summary>
/// Apple/Google sign-in cannot serve this request: the provider is not
/// configured, or the request would create a new account while
/// <c>SocialSignIn:Enabled</c> is false. The API answers 404 (not offered).
/// </summary>
public sealed class SocialSignInUnavailableException() : Exception("Social sign-in not offered for this request");
