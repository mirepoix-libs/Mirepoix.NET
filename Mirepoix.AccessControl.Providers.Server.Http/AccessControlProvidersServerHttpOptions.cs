using Mirepoix.AccessControl.Protocol.Http;

namespace Mirepoix.AccessControl.Providers.Server.Http;

/// <summary>
/// Selects providers HTTP hydrate slices and their shared route settings.
/// </summary>
public sealed class AccessControlProvidersServerHttpOptions
{
    /// <summary>Gets or sets the shared route prefix. Default is <c>/access-control</c>.</summary>
    public string RoutePrefix { get; set; } = AccessControlHttpRoutes.DefaultPrefix;

    /// <summary>Gets or sets the optional authorization policy applied to every hydrate endpoint.</summary>
    public string? AuthorizationPolicy { get; set; }

    internal bool SubjectEnabled { get; private set; }
    internal bool ResourceEnabled { get; private set; }
    internal bool ContextEnabled { get; private set; }

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
}
