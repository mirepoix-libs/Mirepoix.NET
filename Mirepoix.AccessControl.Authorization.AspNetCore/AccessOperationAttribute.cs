namespace Mirepoix.AccessControl.Authorization.AspNetCore;

/// <summary>
/// Attaches the required operation string to an MVC action or endpoint.
/// HTTP PEPs fail closed (forbid) when this metadata is missing or blank.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AccessOperationAttribute : Attribute
{
    /// <summary>
    /// Creates metadata holding <paramref name="operation"/> (parsed later via <see cref="Operation.Parse"/>).
    /// </summary>
    /// <param name="operation">Operation string such as <c>doc:edit</c>. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="operation"/> is null.</exception>
    public AccessOperationAttribute(string operation)
    {
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
    }

    /// <summary>
    /// Holds the raw operation string read by the HTTP PEP.
    /// </summary>
    public string Operation { get; }
}
