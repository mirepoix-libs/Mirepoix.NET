using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Reads each configured enforcement catalog and returns apps that answered plus names that failed.
/// </summary>
public interface IEnforcementCatalogClient
{
    /// <summary>
    /// Requests each app's catalog and collects successes and failures.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels the whole pull. A canceled token propagates.
    /// A client timeout, which cancels without this token, is recorded as that app failing.
    /// </param>
    /// <returns>Apps whose catalogs were read, and the names of apps that did not answer.</returns>
    Task<OperationCatalogPull> PullAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Reads each configured enforcement operation catalog.
/// Remote apps GET <c>{origin}{PublishedOperation.EnforcementPath}</c> with camelCase JSON.
/// Local apps read <see cref="IPublishedOperationSource"/>.
/// Non-success, null JSON, a missing local source, and any exception other than caller cancellation
/// add that app name to the failed list. A timeout is an <see cref="OperationCanceledException"/>
/// whose token is not the caller token, so it is recorded as that app failing.
/// </summary>
public sealed class EnforcementCatalogClient : IEnforcementCatalogClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;
    private readonly OperationCatalogOptions _options;
    private readonly IServiceProvider? _services;

    /// <summary>
    /// Creates a client that reads each app in <paramref name="options"/>.
    /// </summary>
    /// <param name="http">Client used for each remote catalog GET. The caller owns its lifetime.</param>
    /// <param name="options">Apps requested in list order. Origins lose a trailing slash before the path is appended.</param>
    /// <param name="services">
    /// Resolves <see cref="IPublishedOperationSource"/> for local apps.
    /// Null, or a missing source, records that local app in <c>FailedApps</c>.
    /// </param>
    public EnforcementCatalogClient(HttpClient http, OperationCatalogOptions options, IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        _http = http;
        _options = options;
        _services = services;
    }

    /// <inheritdoc />
    public async Task<OperationCatalogPull> PullAsync(CancellationToken cancellationToken)
    {
        var apps = new List<EnforcementAppCatalog>();
        var failed = new List<string>();
        foreach (var app in _options.Apps)
        {
            if (app.IsLocal)
            {
                ReadLocal(app, apps, failed, cancellationToken);
                continue;
            }

            await ReadRemote(app, apps, failed, cancellationToken).ConfigureAwait(false);
        }

        return new OperationCatalogPull(apps, failed);
    }

    private void ReadLocal(
        EnforcementApp app,
        List<EnforcementAppCatalog> apps,
        List<string> failed,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var operations = _services?.GetService<IPublishedOperationSource>()?.List();
            if (operations is null)
            {
                failed.Add(app.Name);
                return;
            }

            apps.Add(new EnforcementAppCatalog(app.Name, operations));
        }
        catch (Exception)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;

            failed.Add(app.Name);
        }
    }

    private async Task ReadRemote(
        EnforcementApp app,
        List<EnforcementAppCatalog> apps,
        List<string> failed,
        CancellationToken cancellationToken)
    {
        var url = app.Origin!.TrimEnd('/') + PublishedOperation.EnforcementPath;
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                failed.Add(app.Name);
                return;
            }

            var operations = await response.Content
                .ReadFromJsonAsync<List<PublishedOperation>>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (operations is null)
            {
                failed.Add(app.Name);
                return;
            }

            apps.Add(new EnforcementAppCatalog(app.Name, operations));
        }
        catch (Exception)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;

            failed.Add(app.Name);
        }
    }
}
