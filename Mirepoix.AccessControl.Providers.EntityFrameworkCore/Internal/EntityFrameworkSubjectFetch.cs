using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Fetches a mapped subject entity via <c>DbSet&lt;T&gt;</c> and an id equality expression.
/// Uses reflection to invoke a generic core with <see cref="SubjectEntityMap.ClrType"/>.
/// Id string is coerced to the id property type (string, Guid, enum, or ChangeType).
/// Returns null when no row matches (probe miss). Does not use <see cref="SubjectStorageHints"/>
/// (EF model metadata defines tables/columns).
/// </summary>
internal static class EntityFrameworkSubjectFetch
{
    /// <summary>
    /// Finds an entity for <paramref name="map"/> by <paramref name="subjectId"/>, or null.
    /// </summary>
    public static Task<object?> FindAsync(
        DbContext db,
        SubjectEntityMap map,
        string subjectId,
        CancellationToken cancellationToken)
    {
        var method = typeof(EntityFrameworkSubjectFetch)
            .GetMethod(nameof(FindCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(map.ClrType);

        return (Task<object?>)method.Invoke(null, new object[] { db, map, subjectId, cancellationToken })!;
    }

    private static async Task<object?> FindCoreAsync<T>(
        DbContext db,
        SubjectEntityMap map,
        string subjectId,
        CancellationToken cancellationToken)
        where T : class
    {
        var typedId = ConvertId(subjectId, map.IdMember.PropertyType);
        var parameter = Expression.Parameter(typeof(T), "e");
        var property = Expression.Property(parameter, map.IdMember);
        Expression constant = Expression.Constant(typedId);
        if (constant.Type != map.IdMember.PropertyType)
            constant = Expression.Convert(constant, map.IdMember.PropertyType);
        var equals = Expression.Equal(property, constant);
        var lambda = Expression.Lambda<Func<T, bool>>(equals, parameter);

        return await db.Set<T>()
            .AsNoTracking()
            .FirstOrDefaultAsync(lambda, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Coerces a subject id string to <paramref name="targetType"/> for the EF predicate.
    /// </summary>
    internal static object ConvertId(string subjectId, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying == typeof(string))
            return subjectId;
        if (underlying == typeof(Guid))
            return Guid.Parse(subjectId);
        if (underlying.IsEnum)
            return Enum.Parse(underlying, subjectId, ignoreCase: true);

        return Convert.ChangeType(subjectId, underlying, CultureInfo.InvariantCulture)!;
    }
}
