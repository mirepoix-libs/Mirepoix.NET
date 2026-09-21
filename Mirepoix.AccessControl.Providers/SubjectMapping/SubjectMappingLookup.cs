namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Orchestrates hint and probe selection for mapped subject hydration.
/// Adapters supply <c>fetchAsync</c> (map and id yield entity or null); this type selects which maps to try
/// and builds the <see cref="Subject"/> via <see cref="SubjectFactory"/>.
/// </summary>
/// <remarks>
/// Resolve algorithm:
/// <list type="bullet">
/// <item><description>
/// If the partial has a non-empty hint attribute (<see cref="SubjectMappingOptions.HintAttributeName"/>):
/// consider only maps where <see cref="SubjectEntityMap.MatchesTypeHint"/> is true. Fetch each candidate
/// in order. For fixed-type maps, first non-null entity wins. For discriminator maps, the fetched row's
/// discriminator must equal the hint. Unknown hint or no matching row: <see cref="KeyNotFoundException"/>.
/// Hint miss does not fall back to unhinted probing.
/// </description></item>
/// <item><description>
/// If no hint: probe <see cref="SubjectMappingOptions.Maps"/> in order; first non-null fetch wins.
/// </description></item>
/// <item><description>
/// Empty maps, blank subject id, or total miss: throw (<see cref="InvalidOperationException"/> or
/// <see cref="KeyNotFoundException"/>). Callers typically surface miss as hydration failure.
/// </description></item>
/// </list>
/// </remarks>
public static class SubjectMappingLookup
{
    /// <summary>
    /// Hydrates <paramref name="partial"/> using <paramref name="options"/> and <paramref name="fetchAsync"/>.
    /// </summary>
    /// <param name="partial">Partial subject (id required; optional hint attribute).</param>
    /// <param name="options">Configured maps and hint attribute name.</param>
    /// <param name="fetchAsync">
    /// Adapter callback: given a map and subject id, return the CLR entity or null if not in that store.
    /// </param>
    /// <param name="cancellationToken">Passed to <paramref name="fetchAsync"/>.</param>
    /// <returns>Hydrated subject from the winning map.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="options"/> has no maps.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when id is missing/blank or no entity matches.</exception>
    public static async Task<Subject> HydrateAsync(
        Subject partial,
        SubjectMappingOptions options,
        Func<SubjectEntityMap, string, CancellationToken, Task<object?>> fetchAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(partial);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fetchAsync);

        if (!options.HasMaps)
            throw new InvalidOperationException("SubjectMappingOptions has no maps.");

        var id = partial.Id;
        if (string.IsNullOrWhiteSpace(id))
            throw new KeyNotFoundException("Subject id was missing.");

        if (TryGetHint(partial, options.HintAttributeName, out var hint))
        {
            var candidates = options.Maps.Where(m => m.MatchesTypeHint(hint)).ToList();
            if (candidates.Count == 0)
                throw new KeyNotFoundException($"Subject '{id}' was not found.");

            foreach (var map in candidates)
            {
                var entity = await fetchAsync(map, id, cancellationToken).ConfigureAwait(false);
                if (entity is null)
                    continue;

                var subject = SubjectFactory.Create(entity, map);
                if (map.FixedTypeValue is not null || DiscriminatorMatches(map, entity, hint))
                    return subject;
            }

            throw new KeyNotFoundException($"Subject '{id}' was not found.");
        }

        foreach (var map in options.Maps)
        {
            var entity = await fetchAsync(map, id, cancellationToken).ConfigureAwait(false);
            if (entity is null)
                continue;

            return SubjectFactory.Create(entity, map);
        }

        throw new KeyNotFoundException($"Subject '{id}' was not found.");
    }

    private static bool TryGetHint(Subject partial, string hintAttributeName, out string hint)
    {
        hint = "";
        if (partial.Attributes is null)
            return false;

        if (!partial.Attributes.TryGetValue(hintAttributeName, out var raw) || raw is null)
            return false;

        var coerced = SubjectFactory.CoerceId(raw);
        if (string.IsNullOrWhiteSpace(coerced))
            return false;

        hint = coerced;
        return true;
    }

    private static bool DiscriminatorMatches(SubjectEntityMap map, object entity, string hint)
    {
        if (map.FixedTypeValue is not null)
            return string.Equals(map.FixedTypeValue, hint, StringComparison.Ordinal);

        if (map.DiscriminatorMember is null)
            return true;

        var value = SubjectFactory.CoerceId(map.DiscriminatorMember.GetValue(entity));
        return string.Equals(value, hint, StringComparison.Ordinal);
    }
}
