namespace Mirepoix.AccessControl.Protocol.Http;

/// <summary>
/// Route constants for the remote access-control HTTP check endpoint.
/// </summary>
public static class AccessControlHttpRoutes
{
    /// <summary>Default route group prefix for engine HTTP endpoints.</summary>
    public const string DefaultPrefix = "/access-control";

    /// <summary>Relative check path segment under <see cref="DefaultPrefix"/>.</summary>
    public const string CheckRelative = "/check";

    /// <summary>Absolute check path: <c>/access-control/check</c>.</summary>
    public const string AbsoluteCheckPath = DefaultPrefix + CheckRelative;
}
