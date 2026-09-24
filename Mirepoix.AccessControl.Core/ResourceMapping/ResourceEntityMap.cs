using System.Reflection;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Maps a CLR domain type to an access-control <see cref="Resource"/>.
/// </summary>
public sealed class ResourceEntityMap
{
    /// <summary>Gets the mapped CLR type.</summary>
    public required Type ClrType { get; init; }

    /// <summary>Gets the access-control resource type.</summary>
    public required string Type { get; init; }

    /// <summary>Gets the property that supplies <see cref="Resource.Id"/>.</summary>
    public required PropertyInfo IdMember { get; init; }

    /// <summary>Gets the optional property exported as <see cref="ResourceAttributeNames.OwnerId"/>.</summary>
    public PropertyInfo? OwnerMember { get; init; }

    /// <summary>Gets properties exported as resource attributes.</summary>
    public required IReadOnlyList<MappedResourceAttributeMember> AttributeMembers { get; init; }

    /// <summary>Gets the domain entity loader.</summary>
    public required Func<string, CancellationToken, Task<object?>> Load { get; init; }
}

/// <summary>
/// Describes one domain property exported as a resource attribute.
/// </summary>
public sealed class MappedResourceAttributeMember
{
    /// <summary>Gets the source property.</summary>
    public required PropertyInfo Member { get; init; }

    /// <summary>Gets the resource attribute key.</summary>
    public required string AttributeName { get; init; }
}
