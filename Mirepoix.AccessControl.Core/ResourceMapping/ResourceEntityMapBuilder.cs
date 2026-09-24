using System.Linq.Expressions;
using System.Reflection;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Builds a resource map for <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">Domain resource type.</typeparam>
public sealed class ResourceEntityMapBuilder<T>
    where T : class
{
    private string? _type;
    private PropertyInfo? _idMember;
    private PropertyInfo? _ownerMember;
    private Func<string, CancellationToken, Task<T?>>? _load;
    private readonly HashSet<string> _excluded = new(StringComparer.Ordinal);
    private readonly HashSet<string> _includeOnly = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _renames = new(StringComparer.Ordinal);
    private bool _includeOnlyMode;

    /// <summary>Sets the required access-control resource type.</summary>
    public ResourceEntityMapBuilder<T> Type(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        _type = type;
        return this;
    }

    /// <summary>Sets the required resource id property.</summary>
    public ResourceEntityMapBuilder<T> Id(Expression<Func<T, object?>> property)
    {
        _idMember = GetProperty(property);
        return this;
    }

    /// <summary>Sets the optional property exported only as <c>ownerId</c>.</summary>
    public ResourceEntityMapBuilder<T> Owner(Expression<Func<T, object?>> property)
    {
        if (_ownerMember is not null)
            throw new InvalidOperationException($"Resource map for '{typeof(T).Name}' may declare Owner(...) only once.");

        _ownerMember = GetProperty(property);
        return this;
    }

    /// <summary>
    /// Includes a property and switches attribute selection to include-only mode.
    /// </summary>
    public ResourceEntityMapBuilder<T> Include(
        Expression<Func<T, object?>> property,
        string? attributeName = null)
    {
        var member = GetProperty(property);
        _includeOnlyMode = true;
        _includeOnly.Add(member.Name);
        if (attributeName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);
            _renames[member.Name] = attributeName;
        }

        return this;
    }

    /// <summary>
    /// Includes a property as an attribute and switches to include-only mode.
    /// </summary>
    public ResourceEntityMapBuilder<T> Attribute(
        Expression<Func<T, object?>> property,
        string? attributeName = null) =>
        Include(property, attributeName);

    /// <summary>Excludes a property from attribute export.</summary>
    public ResourceEntityMapBuilder<T> Exclude(Expression<Func<T, object?>> property)
    {
        _excluded.Add(GetProperty(property).Name);
        return this;
    }

    /// <summary>Renames a property's exported attribute key.</summary>
    public ResourceEntityMapBuilder<T> Rename(
        Expression<Func<T, object?>> property,
        string attributeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);
        _renames[GetProperty(property).Name] = attributeName;
        return this;
    }

    /// <summary>Sets the required asynchronous entity loader.</summary>
    public ResourceEntityMapBuilder<T> Load(
        Func<string, CancellationToken, Task<T?>> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        _load = load;
        return this;
    }

    /// <summary>Builds and validates the immutable resource map.</summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when Type, Id, or Load is missing.
    /// </exception>
    public ResourceEntityMap Build()
    {
        if (string.IsNullOrWhiteSpace(_type))
            throw new InvalidOperationException($"Resource map for '{typeof(T).Name}' requires Type(...).");
        if (_idMember is null)
            throw new InvalidOperationException($"Resource map for '{typeof(T).Name}' requires Id(...).");
        if (_load is null)
            throw new InvalidOperationException($"Resource map for '{typeof(T).Name}' requires Load(...).");

        var idMember = _idMember;
        var ownerMember = _ownerMember;
        var attributeMembers = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
                property.CanRead &&
                property.GetIndexParameters().Length == 0 &&
                property.Name != idMember.Name &&
                property.Name != ownerMember?.Name &&
                !_excluded.Contains(property.Name) &&
                (!_includeOnlyMode || _includeOnly.Contains(property.Name)))
            .Select(property => new MappedResourceAttributeMember
            {
                Member = property,
                AttributeName = _renames.TryGetValue(property.Name, out var renamed)
                    ? renamed
                    : property.Name,
            })
            .ToArray();

        var load = _load;
        return new ResourceEntityMap
        {
            ClrType = typeof(T),
            Type = _type,
            IdMember = idMember,
            OwnerMember = ownerMember,
            AttributeMembers = attributeMembers,
            Load = async (id, cancellationToken) => await load(id, cancellationToken).ConfigureAwait(false),
        };
    }

    private static PropertyInfo GetProperty(Expression<Func<T, object?>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        Expression body = expression.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            body = unary.Operand;

        if (body is MemberExpression { Member: PropertyInfo property })
            return property;

        throw new ArgumentException("Expression must be a property access.", nameof(expression));
    }
}
