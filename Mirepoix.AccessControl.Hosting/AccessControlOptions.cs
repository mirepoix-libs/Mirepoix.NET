namespace Mirepoix.AccessControl.Hosting;

/// <summary>
/// Holds shared Hosting constants used by DI registration and endpoint helpers.
/// </summary>
public static class AccessControlOptions
{
    /// <summary>
    /// Names the ASP.NET authorization policy registered by <see cref="AccessControlBuilder"/>
    /// and applied by <see cref="AccessControlEndpointExtensions.RequireAccessControl{TBuilder}"/>.
    /// </summary>
    public const string PolicyName = "MirepoixAccess";
}
