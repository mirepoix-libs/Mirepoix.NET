using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Rejects attribute atoms the published catalog cannot support.
/// </summary>
/// <remarks>
/// Inspects <see cref="AttributeValueAtom"/>, <see cref="AttributeEqualsAttributeAtom"/>,
/// and <see cref="SubjectIdEqualsAttributeAtom"/> only. Other atoms are skipped.
/// Subject and resource rows match <c>(target, type, key)</c> with ordinal comparison.
/// Context rows match <c>(context, key)</c> and ignore type.
/// An empty provider list leaves subject and resource atoms ungated.
/// An empty enforcement list leaves context atoms ungated.
/// </remarks>
public sealed class AttributeCatalogValidator
{
    /// <summary>
    /// Creates a validator that holds no catalog of its own.
    /// </summary>
    public AttributeCatalogValidator()
    {
    }

    /// <summary>
    /// Checks every attribute atom in <paramref name="set"/> against <paramref name="published"/>.
    /// </summary>
    /// <param name="set">Policy set whose atoms are walked in order.</param>
    /// <param name="published">Catalog rows compared with ordinal equality.</param>
    /// <param name="gateSubjectAndResource">
    /// When false, subject and resource atoms pass without a catalog match.
    /// Pass false when the provider app list is empty.
    /// </param>
    /// <param name="gateContext">
    /// When false, context atoms pass without a catalog match.
    /// Pass false when the enforcement app list is empty.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when a gated atom names a key or type absent from <paramref name="published"/>.
    /// </exception>
    public void EnsureSupported(
        PolicySet set,
        IReadOnlyCollection<PublishedAttribute> published,
        bool gateSubjectAndResource = true,
        bool gateContext = true)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(published);

        foreach (var policy in set.Policies)
        {
            foreach (var atom in policy.Atoms)
            {
                switch (atom)
                {
                    case AttributeValueAtom value:
                        Ensure(value.Target, value.Type, value.Key);
                        break;
                    case SubjectIdEqualsAttributeAtom subjectId:
                        Ensure(subjectId.Target, subjectId.Type, subjectId.Key);
                        break;
                    case AttributeEqualsAttributeAtom equals:
                        Ensure(equals.LeftTarget, equals.LeftType, equals.LeftKey);
                        Ensure(equals.RightTarget, equals.RightType, equals.RightKey);
                        break;
                }
            }
        }

        void Ensure(AttributeTarget target, string? type, string key)
        {
            var gated = target == AttributeTarget.Context ? gateContext : gateSubjectAndResource;
            if (!gated)
                return;

            foreach (var row in published)
            {
                if (row.Target != target || !string.Equals(row.Key, key, StringComparison.Ordinal))
                    continue;

                if (target == AttributeTarget.Context || string.Equals(row.Type, type, StringComparison.Ordinal))
                    return;
            }

            var message = target == AttributeTarget.Context
                ? $"Context attribute '{key}' is not published."
                : $"Attribute target '{target}' type '{type}' key '{key}' is not published.";
            throw new ArgumentException(message, nameof(set));
        }
    }
}
