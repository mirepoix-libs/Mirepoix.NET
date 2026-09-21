using System.Linq.Expressions;
using System.Reflection;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Builds a <see cref="SubjectEntityMap"/> over <typeparamref name="T"/>.
/// Defaults: all other public readable instance properties become attributes (excluding id, roles,
/// discriminator, and anything <see cref="Exclude"/>d). Calling <see cref="Include"/> switches to
/// include-only mode for attributes. <see cref="Build"/> is internal; use
/// <see cref="SubjectMappingOptions.MapEntity{T}"/>.
/// </summary>
/// <typeparam name="T">CLR entity type being mapped.</typeparam>
public sealed class SubjectEntityMapBuilder<T>
    where T : class
{
    private PropertyInfo? _idMember;
    private readonly HashSet<string> _excluded = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _renames = new(StringComparer.Ordinal);
    private readonly List<PropertyInfo> _roleMembers = new();
    private readonly HashSet<string> _includeOnly = new(StringComparer.Ordinal);
    private bool _includeOnlyMode;
    private string? _fixedTypeValue;
    private PropertyInfo? _discriminatorMember;
    private SubjectTypeDisposition _typeDisposition = SubjectTypeDisposition.Attribute;
    private string _typeAttributeName = SubjectMappingOptions.DefaultHintAttributeName;
    private string? _schema;
    private string? _table;
    private readonly Dictionary<string, string> _columnOverrides = new(StringComparer.Ordinal);

    /// <summary>
    /// Sets the id property via expression.
    /// </summary>
    /// <param name="property">Property access expression.</param>
    /// <returns>This builder.</returns>
    public SubjectEntityMapBuilder<T> Id(Expression<Func<T, object?>> property)
    {
        _idMember = GetProperty(property);
        return this;
    }

    /// <summary>
    /// Sets the id property by name (public instance, case-insensitive).
    /// </summary>
    /// <param name="propertyName">Property name.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the property does not exist.</exception>
    public SubjectEntityMapBuilder<T> Id(string propertyName)
    {
        _idMember = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new InvalidOperationException($"Type '{typeof(T).Name}' has no public property '{propertyName}'.");
        return this;
    }

    /// <summary>
    /// Excludes a property from attribute export.
    /// </summary>
    /// <param name="property">Property to exclude.</param>
    /// <returns>This builder.</returns>
    public SubjectEntityMapBuilder<T> Exclude(Expression<Func<T, object?>> property)
    {
        _excluded.Add(GetProperty(property).Name);
        return this;
    }

    /// <summary>
    /// Includes a property as an attribute and switches the builder to include-only mode
    /// (only explicitly included properties become attributes, aside from id/roles/discriminator rules).
    /// </summary>
    /// <param name="property">Property to include.</param>
    /// <param name="attributeName">Optional attribute key; defaults to the property name.</param>
    /// <returns>This builder.</returns>
    public SubjectEntityMapBuilder<T> Include(Expression<Func<T, object?>> property, string? attributeName = null)
    {
        _includeOnlyMode = true;
        var prop = GetProperty(property);
        _includeOnly.Add(prop.Name);
        if (attributeName is not null)
            _renames[prop.Name] = attributeName;
        return this;
    }

    /// <summary>
    /// Renames a property's attribute key without changing include/exclude mode.
    /// </summary>
    /// <param name="property">Property to rename.</param>
    /// <param name="attributeName">Non-empty attribute key.</param>
    /// <returns>This builder.</returns>
    public SubjectEntityMapBuilder<T> Rename(Expression<Func<T, object?>> property, string attributeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);
        _renames[GetProperty(property).Name] = attributeName;
        return this;
    }

    /// <summary>
    /// Marks a property as contributing to <see cref="Subject.Roles"/> (excluded from attributes).
    /// </summary>
    /// <param name="property">Role source property.</param>
    /// <returns>This builder.</returns>
    public SubjectEntityMapBuilder<T> Roles(Expression<Func<T, object?>> property)
    {
        _roleMembers.Add(GetProperty(property));
        return this;
    }

    /// <summary>
    /// Sets a fixed type token for this map (multi-table / multi-type registration).
    /// </summary>
    /// <param name="typeValue">Non-empty type token (e.g. <c>employee</c>).</param>
    /// <returns>This builder.</returns>
    public SubjectEntityMapBuilder<T> Type(string typeValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeValue);
        _fixedTypeValue = typeValue;
        return this;
    }

    /// <summary>
    /// Sets a per-row discriminator property (excluded from attributes; applied via type disposition).
    /// </summary>
    /// <param name="property">Discriminator property.</param>
    /// <returns>This builder.</returns>
    public SubjectEntityMapBuilder<T> Discriminator(Expression<Func<T, object?>> property)
    {
        _discriminatorMember = GetProperty(property);
        return this;
    }

    /// <summary>
    /// Sets the attribute key used when type disposition writes an attribute.
    /// </summary>
    /// <param name="name">Non-empty attribute name.</param>
    /// <returns>This builder.</returns>
    public SubjectEntityMapBuilder<T> TypeAttributeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _typeAttributeName = name;
        return this;
    }

    /// <summary>Writes the type value to attributes only (default).</summary>
    /// <returns>This builder.</returns>
    public SubjectEntityMapBuilder<T> TypeAsAttribute()
    {
        _typeDisposition = SubjectTypeDisposition.Attribute;
        return this;
    }

    /// <summary>Writes the type value to roles only.</summary>
    /// <returns>This builder.</returns>
    public SubjectEntityMapBuilder<T> TypeAsRole()
    {
        _typeDisposition = SubjectTypeDisposition.Role;
        return this;
    }

    /// <summary>Writes the type value to both roles and attributes.</summary>
    /// <returns>This builder.</returns>
    public SubjectEntityMapBuilder<T> TypeAsBoth()
    {
        _typeDisposition = SubjectTypeDisposition.Both;
        return this;
    }

    /// <summary>
    /// Sets ADO storage table (and optional schema) hints.
    /// </summary>
    /// <param name="table">Non-empty table name.</param>
    /// <param name="schema">Optional schema; null means unspecified.</param>
    /// <returns>This builder.</returns>
    public SubjectEntityMapBuilder<T> ToTable(string table, string? schema = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        _table = table;
        _schema = schema;
        return this;
    }

    /// <summary>
    /// Overrides the column name for a property (ADO adapters).
    /// </summary>
    /// <param name="property">Property whose column name differs from the property name.</param>
    /// <param name="columnName">Non-empty column name.</param>
    /// <returns>This builder.</returns>
    public SubjectEntityMapBuilder<T> Column(Expression<Func<T, object?>> property, string columnName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        _columnOverrides[GetProperty(property).Name] = columnName;
        return this;
    }

    /// <summary>
    /// Materializes the immutable <see cref="SubjectEntityMap"/> (package helper).
    /// Id defaults to a public <c>Id</c> property when <see cref="Id(Expression{Func{T, object?}})"/> was not called.
    /// </summary>
    /// <returns>Built map.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no id member can be resolved.</exception>
    internal SubjectEntityMap Build()
    {
        var id = _idMember
            ?? typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"Subject map for '{typeof(T).Name}' requires Id(...); no conventional 'Id' property found.");

        var roleNames = new HashSet<string>(_roleMembers.Select(r => r.Name), StringComparer.Ordinal);
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToList();

        var attributeMembers = new List<MappedAttributeMember>();
        foreach (var prop in properties)
        {
            if (prop.Name == id.Name)
                continue;
            if (roleNames.Contains(prop.Name))
                continue;
            if (_discriminatorMember is not null && prop.Name == _discriminatorMember.Name)
                continue;
            if (_excluded.Contains(prop.Name))
                continue;
            if (_includeOnlyMode && !_includeOnly.Contains(prop.Name))
                continue;

            var attrName = _renames.TryGetValue(prop.Name, out var renamed) ? renamed : prop.Name;
            attributeMembers.Add(new MappedAttributeMember { Member = prop, AttributeName = attrName });
        }

        SubjectStorageHints? hints = null;
        if (_table is not null || _schema is not null || _columnOverrides.Count > 0)
        {
            hints = new SubjectStorageHints
            {
                Schema = _schema,
                Table = _table,
                ColumnOverrides = new Dictionary<string, string>(_columnOverrides, StringComparer.Ordinal),
            };
        }

        return new SubjectEntityMap
        {
            ClrType = typeof(T),
            IdMember = id,
            AttributeMembers = attributeMembers,
            RoleMembers = _roleMembers.ToArray(),
            FixedTypeValue = _fixedTypeValue,
            DiscriminatorMember = _discriminatorMember,
            TypeDisposition = _typeDisposition,
            TypeAttributeName = _typeAttributeName,
            StorageHints = hints,
        };
    }

    private static PropertyInfo GetProperty(Expression<Func<T, object?>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var body = expression.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            body = unary.Operand;

        if (body is MemberExpression { Member: PropertyInfo prop })
            return prop;

        throw new ArgumentException("Expression must be a property access.", nameof(expression));
    }
}
