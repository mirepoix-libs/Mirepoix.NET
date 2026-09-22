using System.Text;
using System.Text.Json;
using Mirepoix.AccessControl.Protocol.Http;

namespace Mirepoix.AccessControl.Engine.Client.Http;

/// <summary>
/// Remote <see cref="IAccessChecker"/> that POSTs partial requests to a PDP HTTP endpoint.
/// </summary>
public sealed class HttpAccessChecker : IAccessChecker
{
    private readonly HttpClient _http;

    /// <summary>Creates a checker that uses <paramref name="http"/> (typically with <see cref="HttpClient.BaseAddress"/> set).</summary>
    public HttpAccessChecker(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    /// <inheritdoc />
    public async Task<AccessDecision> CheckAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dto = AuthorizationRequestDto.FromDomain(request);
        using var content = new StringContent(
            JsonSerializer.Serialize(dto, AccessControlHttpJson.DefaultOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _http.PostAsync(
            new Uri(AccessControlHttpRoutes.AbsoluteCheckPath, UriKind.Relative),
            content,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var decisionDto = await JsonSerializer.DeserializeAsync<AccessDecisionDto>(
            stream,
            AccessControlHttpJson.DefaultOptions,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Empty PDP response.");

        return decisionDto.ToDomain();
    }
}
