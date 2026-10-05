using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Protocol.Http;

namespace Mirepoix.AccessControl.Providers.Server.Http;

/// <summary>
/// Selects providers HTTP hydrate slices, the attribute catalog GET, and their shared route settings.
/// </summary>
public sealed class AccessControlProvidersServerHttpOptions
{
    /// <summary>Gets or sets the shared route prefix. Default is <c>/access-control</c>.</summary>
    public string RoutePrefix { get; set; } = AccessControlHttpRoutes.DefaultPrefix;

    /// <summary>
    /// Gets or sets the host authorization policy name applied to every mapped route. Required at map time.
    /// </summary>
    /// <remarks>
    /// <c>MapAccessControlProviders</c> throws <see cref="InvalidOperationException"/> when this value is null or white space.
    /// The message is "Access-control HTTP routes require an authorization policy."
    /// This package does not register an authentication scheme.
    /// </remarks>
    public string? AuthorizationPolicy { get; set; }

    internal bool SubjectEnabled { get; private set; }
    internal bool ResourceEnabled { get; private set; }
    internal bool ContextEnabled { get; private set; }
    internal bool AttributeCatalogEnabled { get; private set; }

    /// <summary>Enables subject hydrate endpoint.</summary>
    public AccessControlProvidersServerHttpOptions AddSubject()
    {
        SubjectEnabled = true;
        return this;
    }

    /// <summary>Enables resource hydrate endpoint.</summary>
    public AccessControlProvidersServerHttpOptions AddResource()
    {
        ResourceEnabled = true;
        return this;
    }

    /// <summary>Enables context hydrate endpoint.</summary>
    public AccessControlProvidersServerHttpOptions AddContext()
    {
        ContextEnabled = true;
        return this;
    }

    /// <summary>
    /// Enables <c>GET</c> <see cref="PublishedAttribute.CatalogPath"/> for the live subject and resource catalog.
    /// That path is fixed. <see cref="RoutePrefix"/> does not move it. This slice may be the only slice.
    /// </summary>
    public AccessControlProvidersServerHttpOptions AddAttributeCatalog()
    {
        AttributeCatalogEnabled = true;
        return this;
    }
}
