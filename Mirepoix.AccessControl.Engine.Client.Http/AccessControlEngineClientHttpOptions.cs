namespace Mirepoix.AccessControl.Engine.Client.Http;

/// <summary>
/// Options for the remote engine HTTP access checker (PEP client).
/// </summary>
public sealed class AccessControlEngineClientHttpOptions
{
    /// <summary>PDP host root (e.g. <c>https://pdp.example/</c>). Required.</summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>Optional per-client configuration applied after <see cref="BaseAddress"/> is set.</summary>
    public Action<HttpClient>? ConfigureHttpClient { get; set; }
}
