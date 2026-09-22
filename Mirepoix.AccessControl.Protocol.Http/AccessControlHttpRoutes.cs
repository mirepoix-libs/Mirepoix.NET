namespace Mirepoix.AccessControl.Protocol.Http;

/// <summary>
/// Route constants for remote access-control HTTP endpoints (PDP check and PIP hydrate).
/// </summary>
public static class AccessControlHttpRoutes
{
    /// <summary>Default route group prefix for access-control HTTP endpoints.</summary>
    public const string DefaultPrefix = "/access-control";

    /// <summary>Relative check path segment under <see cref="DefaultPrefix"/>.</summary>
    public const string CheckRelative = "/check";

    /// <summary>Absolute check path: <c>/access-control/check</c>.</summary>
    public const string AbsoluteCheckPath = DefaultPrefix + CheckRelative;

    /// <summary>Relative subject hydrate path under <see cref="DefaultPrefix"/>.</summary>
    public const string ProvidersSubjectHydrateRelative = "/providers/subject/hydrate";

    /// <summary>Relative resource hydrate path under <see cref="DefaultPrefix"/>.</summary>
    public const string ProvidersResourceHydrateRelative = "/providers/resource/hydrate";

    /// <summary>Relative context hydrate path under <see cref="DefaultPrefix"/>.</summary>
    public const string ProvidersContextHydrateRelative = "/providers/context/hydrate";

    /// <summary>Absolute subject hydrate path: <c>/access-control/providers/subject/hydrate</c>.</summary>
    public const string AbsoluteProvidersSubjectHydratePath =
        DefaultPrefix + ProvidersSubjectHydrateRelative;

    /// <summary>Absolute resource hydrate path: <c>/access-control/providers/resource/hydrate</c>.</summary>
    public const string AbsoluteProvidersResourceHydratePath =
        DefaultPrefix + ProvidersResourceHydrateRelative;

    /// <summary>Absolute context hydrate path: <c>/access-control/providers/context/hydrate</c>.</summary>
    public const string AbsoluteProvidersContextHydratePath =
        DefaultPrefix + ProvidersContextHydrateRelative;
}
