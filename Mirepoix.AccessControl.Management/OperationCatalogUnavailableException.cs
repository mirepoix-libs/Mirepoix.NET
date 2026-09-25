namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Signals that policy replacement has no catalog to validate against.
/// </summary>
/// <remarks>
/// Thrown when the pull lists failed apps and <see cref="OperationCatalogSnapshot.HasSnapshot"/> is false.
/// Derives from <see cref="InvalidOperationException"/> so hosts can map it to a retryable failure.
/// </remarks>
public sealed class OperationCatalogUnavailableException : InvalidOperationException
{
    /// <summary>
    /// Creates the exception with <paramref name="message"/>.
    /// </summary>
    /// <param name="message">Explains that no complete catalog is stored yet.</param>
    public OperationCatalogUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates the exception with <paramref name="message"/> and <paramref name="innerException"/>.
    /// </summary>
    /// <param name="message">Explains that no complete catalog is stored yet.</param>
    /// <param name="innerException">Failure that left the catalog unavailable.</param>
    public OperationCatalogUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
