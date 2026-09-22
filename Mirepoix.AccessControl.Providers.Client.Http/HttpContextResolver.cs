using System.Text;
using System.Text.Json;
using Mirepoix.AccessControl.Protocol.Http;
using Mirepoix.AccessControl.Providers;

namespace Mirepoix.AccessControl.Providers.Client.Http;

/// <summary>
/// Remote <see cref="IContextResolver"/> that POSTs a partial context to a PIP HTTP endpoint.
/// </summary>
public sealed class HttpContextResolver : IContextResolver
{
    private readonly HttpClient _http;

    /// <summary>Creates a resolver that uses <paramref name="http"/> (typically with <see cref="HttpClient.BaseAddress"/> set).</summary>
    public HttpContextResolver(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    /// <inheritdoc />
    public async Task<AccessContext> HydrateAsync(AccessContext partial, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(partial);

        var dto = AccessContextDto.FromDomain(partial);
        using var content = new StringContent(
            JsonSerializer.Serialize(dto, AccessControlHttpJson.DefaultOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _http.PostAsync(
            new Uri(AccessControlHttpRoutes.AbsoluteProvidersContextHydratePath, UriKind.Relative),
            content,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<AccessContextDto>(
            stream,
            AccessControlHttpJson.DefaultOptions,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Empty PIP context response.");

        return result.ToDomain();
    }
}
