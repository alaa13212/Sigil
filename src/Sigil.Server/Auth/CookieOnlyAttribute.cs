namespace Sigil.Server.Auth;

/// <summary>Marks an endpoint that only the interface session may call.</summary>
/// <remarks>
/// An API token is handed to scripts, CI variables and environment files, so it must not
/// reach an endpoint that returns a secret such as an ingestion key, a webhook URL or an
/// access token. The UI session keeps full access.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class CookieOnlyAttribute : Attribute;
