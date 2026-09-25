using System.Net.Http.Json;
using System.Text.Json;
using Mirepoix.AccessControl;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// GETs each configured enforcement catalog and returns apps that answered plus names that failed.
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
    /// <returns>Apps whose bodies deserialized, and the names of apps that did not.</returns>
    Task<OperationCatalogPull> PullAsync(CancellationToken cancellationToken);
}

/// <summary>
/// GETs <c>{origin}{PublishedOperation.EnforcementPath}</c> for each configured app.
/// Non-success, null JSON, and any exception other than caller cancellation add that app name
/// to the failed list. A timeout is an <see cref="OperationCanceledException"/> whose token
/// is not the caller token, so it is recorded as that app failing.
/// </summary>
public sealed class EnforcementCatalogClient : IEnforcementCatalogClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;
    private readonly OperationCatalogOptions _options;

    /// <summary>
    /// Creates a client that GETs each app in <paramref name="options"/> through <paramref name="http"/>.
    /// </summary>
    /// <param name="http">Client used for each catalog GET. The caller owns its lifetime.</param>
    /// <param name="options">Apps requested in list order. Origins lose a trailing slash before the path is appended.</param>
    public EnforcementCatalogClient(HttpClient http, OperationCatalogOptions options)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        _http = http;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<OperationCatalogPull> PullAsync(CancellationToken cancellationToken)
    {
        var apps = new List<EnforcementAppCatalog>();
        var failed = new List<string>();
        foreach (var app in _options.Apps)
        {
            var url = app.Origin.TrimEnd('/') + PublishedOperation.EnforcementPath;
            try
            {
                using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    failed.Add(app.Name);
                    continue;
                }

                var operations = await response.Content
                    .ReadFromJsonAsync<List<PublishedOperation>>(JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                if (operations is null)
                {
                    failed.Add(app.Name);
                    continue;
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

        return new OperationCatalogPull(apps, failed);
    }
}
