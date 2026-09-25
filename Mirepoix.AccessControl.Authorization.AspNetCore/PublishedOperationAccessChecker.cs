namespace Mirepoix.AccessControl.Authorization.AspNetCore;

/// <summary>
/// Wraps an <see cref="IAccessChecker"/> and rejects operations that are not in the enforcement catalog.
/// The match is ordinal on <see cref="Operation.Value"/>. After a match, the inner checker runs unchanged.
/// </summary>
public sealed class PublishedOperationAccessChecker : IAccessChecker
{
    private readonly IAccessChecker _inner;
    private readonly PublishedOperationSource _source;

    /// <summary>
    /// Stores the previous checker and the catalog source loaded on each check.
    /// </summary>
    /// <param name="inner">Checker that runs after the operation is published. Local or remote.</param>
    /// <param name="source">Catalog built from endpoints and <c>AddAccessControlOperation</c>.</param>
    public PublishedOperationAccessChecker(IAccessChecker inner, PublishedOperationSource source)
    {
        _inner = inner;
        _source = source;
    }

    /// <summary>
    /// Loads the catalog, throws when <paramref name="request"/> names an unpublished operation, then calls the inner checker.
    /// </summary>
    /// <param name="request">Caller-known subject, resource, operation, and context.</param>
    /// <param name="cancellationToken">Passed to the inner checker. Catalog load is synchronous.</param>
    /// <returns>The inner checker's decision.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no listed operation equals <c>request.Operation.Value</c> by ordinal comparison,
    /// or when catalog load itself rejects a blank or wildcard endpoint operation.
    /// </exception>
    public async Task<AccessDecision> CheckAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        var published = _source.List();
        var value = request.Operation.Value;
        if (!published.Any(item => string.Equals(item.Operation, value, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Operation '{value}' is not published. Map it with AccessOperation metadata or AddAccessControlOperation.");
        }

        return await _inner.CheckAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
