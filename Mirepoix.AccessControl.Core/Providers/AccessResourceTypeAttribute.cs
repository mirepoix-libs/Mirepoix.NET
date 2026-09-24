namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Declares the resource type handled by a resource resolver.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AccessResourceTypeAttribute : Attribute
{
    /// <summary>
    /// Creates resource type metadata for a resolver.
    /// </summary>
    /// <param name="type">Resource type handled by the resolver.</param>
    public AccessResourceTypeAttribute(string type)
    {
        Type = type;
    }

    /// <summary>Gets the resource type handled by the resolver.</summary>
    public string Type { get; }
}
