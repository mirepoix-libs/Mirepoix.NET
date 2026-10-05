using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Reads attribute catalogs for each configured provider and enforcement app.
/// Remote apps GET <c>{origin}{PublishedAttribute.CatalogPath}</c> with camelCase JSON.
/// Local provider apps read <see cref="IPublishedProviderAttributeSource"/>.
/// Local enforcement apps read <see cref="IPublishedEnforcementAttributeSource"/>.
/// Non-success, null JSON, a missing local source, and any exception other than caller cancellation
/// add that app name to the failed list. A timeout is an <see cref="OperationCanceledException"/>
/// whose token is not the caller token, so it is recorded as that app failing.
/// Provider catalogs keep subject and resource rows. Enforcement catalogs keep context rows
/// and store a null type.
/// </summary>
public sealed class AttributeCatalogClient : IAttributeCatalogClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;
    private readonly AttributeCatalogOptions _options;
    private readonly IServiceProvider? _services;

    /// <summary>
    /// Creates a client that reads each app in <paramref name="options"/>.
    /// </summary>
    /// <param name="http">Client used for each remote catalog GET. The caller owns its lifetime.</param>
    /// <param name="options">Apps requested in list order. Origins lose a trailing slash before the path is appended.</param>
    /// <param name="services">
    /// Resolves local catalog sources. Null, or a missing source, records that local app in <c>FailedApps</c>.
    /// </param>
    public AttributeCatalogClient(HttpClient http, AttributeCatalogOptions options, IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        _http = http;
        _options = options;
        _services = services;
    }

    /// <inheritdoc />
    public async Task<AttributeCatalogPull> PullAsync(CancellationToken cancellationToken)
    {
        var providers = new List<AttributeAppCatalog>();
        var enforcement = new List<AttributeAppCatalog>();
        var failed = new List<string>();

        foreach (var app in _options.ProviderApps)
            await ReadApp(app, providers, failed, provider: true, cancellationToken).ConfigureAwait(false);

        foreach (var app in _options.EnforcementApps)
            await ReadApp(app, enforcement, failed, provider: false, cancellationToken).ConfigureAwait(false);

        return new AttributeCatalogPull(providers, enforcement, failed);
    }

    private async Task ReadApp(
        AttributeCatalogApp app,
        List<AttributeAppCatalog> catalogs,
        List<string> failed,
        bool provider,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = app.IsLocal
                ? ReadLocal(provider)
                : await ReadRemote(app.Origin!, cancellationToken).ConfigureAwait(false);
            if (rows is null)
            {
                failed.Add(app.Name);
                return;
            }

            catalogs.Add(new AttributeAppCatalog(app.Name, Filter(rows, provider)));
        }
        catch (Exception)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;

            failed.Add(app.Name);
        }
    }

    private IReadOnlyList<PublishedAttribute>? ReadLocal(bool provider)
    {
        if (_services is null)
            return null;

        if (provider)
            return _services.GetService<IPublishedProviderAttributeSource>()?.List();

        return _services.GetService<IPublishedEnforcementAttributeSource>()?.List();
    }

    private async Task<IReadOnlyList<PublishedAttribute>?> ReadRemote(string origin, CancellationToken cancellationToken)
    {
        var url = origin.TrimEnd('/') + PublishedAttribute.CatalogPath;
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content
            .ReadFromJsonAsync<List<PublishedAttribute>>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private static List<PublishedAttribute> Filter(IReadOnlyList<PublishedAttribute> rows, bool provider)
    {
        var kept = new List<PublishedAttribute>();
        foreach (var row in rows)
        {
            if (provider)
            {
                if (row.Target is AttributeTarget.Subject or AttributeTarget.Resource)
                    kept.Add(row);
                continue;
            }

            if (row.Target == AttributeTarget.Context)
                kept.Add(new PublishedAttribute(AttributeTarget.Context, null, row.Key));
        }

        return kept;
    }
}
