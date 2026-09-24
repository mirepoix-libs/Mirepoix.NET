using System.Text;
using System.Text.Json;
using Mirepoix.AccessControl.Protocol.Http;
using Mirepoix.AccessControl.Providers;

namespace Mirepoix.AccessControl.Providers.Client.Http;

/// <summary>
/// Remote <see cref="IResourceHydrator"/> that POSTs a partial resource to a PIP HTTP endpoint.
/// </summary>
public sealed class HttpResourceHydrator : IResourceHydrator
{
    private readonly HttpClient _http;

    /// <summary>Creates a hydrator that uses <paramref name="http"/> (typically with <see cref="HttpClient.BaseAddress"/> set).</summary>
    public HttpResourceHydrator(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    /// <inheritdoc />
    public async Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(partial);

        var dto = ResourceDto.FromDomain(partial);
        using var content = new StringContent(
            JsonSerializer.Serialize(dto, AccessControlHttpJson.DefaultOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _http.PostAsync(
            new Uri(AccessControlHttpRoutes.AbsoluteProvidersResourceHydratePath, UriKind.Relative),
            content,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<ResourceDto>(
            stream,
            AccessControlHttpJson.DefaultOptions,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Empty PIP resource response.");

        return result.ToDomain();
    }
}
