using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Rejects policy operation patterns the published catalog cannot support.
/// </summary>
/// <remarks>
/// Inspects <see cref="OperationMatchAtom"/> only. Role and other atoms are skipped.
/// Concrete patterns must equal a published string. Partial patterns must align on
/// every concrete segment. All-wildcard patterns are limited by segment count.
/// </remarks>
public sealed class OperationPatternValidator
{
    /// <summary>
    /// Creates a validator that holds no catalog of its own.
    /// </summary>
    public OperationPatternValidator()
    {
    }

    /// <summary>
    /// Checks every <see cref="OperationMatchAtom"/> in <paramref name="set"/> against the catalog.
    /// </summary>
    /// <param name="set">Policy set whose atoms are walked in order.</param>
    /// <param name="publishedOperations">Concrete operation strings compared with ordinal equality.</param>
    /// <param name="maxSegments">Inclusive upper bound for an all-wildcard pattern's segment count.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when a concrete pattern is absent from <paramref name="publishedOperations"/>,
    /// when a partial pattern aligns with none of them, or when an all-wildcard pattern's
    /// segment count is outside 1 through <paramref name="maxSegments"/>.
    /// </exception>
    public void EnsureSupported(PolicySet set, IReadOnlyCollection<string> publishedOperations, int maxSegments)
    {
        foreach (var policy in set.Policies)
        {
            foreach (var atom in policy.Atoms)
            {
                if (atom is OperationMatchAtom match)
                    EnsurePattern(match.Pattern, publishedOperations, maxSegments);
            }
        }
    }

    /// <summary>
    /// Applies the concrete, partial, and all-wildcard rules to one pattern.
    /// </summary>
    /// <param name="pattern">Policy pattern, including <c>*</c> segments.</param>
    /// <param name="publishedOperations">Concrete operation strings used for exact and partial checks.</param>
    /// <param name="maxSegments">Inclusive upper bound for an all-wildcard pattern's segment count.</param>
    private static void EnsurePattern(Operation pattern, IReadOnlyCollection<string> publishedOperations, int maxSegments)
    {
        var segments = pattern.Value.Split(':');
        var wildcards = segments.Count(segment => segment == "*");
        if (wildcards == segments.Length)
        {
            if (segments.Length < 1 || segments.Length > maxSegments)
                throw new ArgumentException($"Wildcard pattern '{pattern.Value}' exceeds max segment count {maxSegments}.", nameof(pattern));
            return;
        }

        if (wildcards == 0)
        {
            if (!publishedOperations.Contains(pattern.Value, StringComparer.Ordinal))
                throw new ArgumentException($"Operation '{pattern.Value}' is not published.", nameof(pattern));
            return;
        }

        foreach (var published in publishedOperations)
        {
            var publishedSegments = published.Split(':');
            var aligned = true;
            for (var i = 0; i < segments.Length; i++)
            {
                if (i >= publishedSegments.Length)
                {
                    aligned = false;
                    break;
                }

                if (segments[i] == "*")
                    continue;
                if (!string.Equals(publishedSegments[i], segments[i], StringComparison.Ordinal))
                {
                    aligned = false;
                    break;
                }
            }

            if (aligned)
                return;
        }

        throw new ArgumentException($"Pattern '{pattern.Value}' does not align with a published operation.", nameof(pattern));
    }
}
