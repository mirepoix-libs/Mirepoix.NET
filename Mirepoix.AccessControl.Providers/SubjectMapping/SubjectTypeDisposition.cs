namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Controls how a type discriminator / fixed type value is applied to the hydrated <see cref="Subject"/>.
/// </summary>
public enum SubjectTypeDisposition
{
    /// <summary>Stores as an attribute only (default), under <see cref="SubjectEntityMap.TypeAttributeName"/>.</summary>
    Attribute = 0,

    /// <summary>Adds the type value to <see cref="Subject.Roles"/> only.</summary>
    Role = 1,

    /// <summary>Adds as both a role and an attribute.</summary>
    Both = 2,
}
