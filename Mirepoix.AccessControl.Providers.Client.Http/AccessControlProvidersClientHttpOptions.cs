using Mirepoix.AccessControl.Protocol.Http;

namespace Mirepoix.AccessControl.Providers.Client.Http;

/// <summary>
/// Selects remote PIP hydrate slices and HttpClient settings for the PDP host.
/// </summary>
public sealed class AccessControlProvidersClientHttpOptions
{
    /// <summary>Gets or sets the PIP host root URI (required absolute).</summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>Optional extra configuration applied after <see cref="BaseAddress"/>.</summary>
    public Action<HttpClient>? ConfigureHttpClient { get; set; }

    internal bool SubjectEnabled { get; private set; }
    internal bool ResourceEnabled { get; private set; }
    internal bool ContextEnabled { get; private set; }

    /// <summary>Registers <see cref="HttpSubjectResolver"/> as <see cref="ISubjectResolver"/>.</summary>
    public AccessControlProvidersClientHttpOptions AddSubject()
    {
        SubjectEnabled = true;
        return this;
    }

    /// <summary>Registers <see cref="HttpResourceHydrator"/> as <see cref="IResourceHydrator"/>.</summary>
    public AccessControlProvidersClientHttpOptions AddResource()
    {
        ResourceEnabled = true;
        return this;
    }

    /// <summary>Registers <see cref="HttpContextResolver"/> as <see cref="IContextResolver"/>.</summary>
    public AccessControlProvidersClientHttpOptions AddContext()
    {
        ContextEnabled = true;
        return this;
    }
}
