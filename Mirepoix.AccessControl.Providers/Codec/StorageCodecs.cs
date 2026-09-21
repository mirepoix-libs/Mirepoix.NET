using System.Text.Json;
using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Providers.Codec;

/// <summary>
/// Encodes <see cref="PolicySet"/> payloads persisted as JSON text
/// (e.g. <c>ac_policy_set.payload_json</c>). Delegates to <see cref="PolicySerializers"/>.
/// </summary>
public static class PolicySetStorageCodec
{
    /// <summary>
    /// Serializes <paramref name="set"/> to storage JSON.
    /// </summary>
    /// <param name="set">Policy set to persist.</param>
    /// <returns>JSON string suitable for the payload column.</returns>
    public static string ToStorageJson(PolicySet set) => PolicySerializers.ToJson(set);

    /// <summary>
    /// Deserializes storage JSON into a <see cref="PolicySet"/>.
    /// </summary>
    /// <param name="json">JSON from the payload column.</param>
    /// <returns>Reconstructed policy set.</returns>
    public static PolicySet FromStorageJson(string json) => PolicySerializers.FromJson(json);
}

/// <summary>
/// Encodes individual attribute values stored as JSON text
/// (e.g. <c>value_json</c> columns). Round-trips primitives, arrays, and falls back to a cloned
/// <see cref="JsonElement"/> for objects. Null in / null out.
/// </summary>
public static class AttributeValueCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Serializes <paramref name="value"/> to JSON text, or null when the value is null.
    /// </summary>
    /// <param name="value">Attribute value to store.</param>
    /// <returns>JSON string, or null.</returns>
    public static string? ToJson(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, JsonOptions);

    /// <summary>
    /// Deserializes <paramref name="json"/> into a CLR value. Null or JSON null becomes null.
    /// Numbers prefer <see cref="int"/> then <see cref="long"/> then <see cref="double"/>.
    /// Arrays become <see cref="List{T}"/> of materialized elements. Object-shaped JSON stays as
    /// <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="json">JSON text from storage; may be null.</param>
    /// <returns>Materialized value, or null.</returns>
    public static object? FromJson(string? json)
    {
        if (json is null)
            return null;

        using var doc = JsonDocument.Parse(json);
        return FromJsonElement(doc.RootElement);
    }

    private static object? FromJsonElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i
                : element.TryGetInt64(out var l) ? l
                : element.GetDouble(),
            JsonValueKind.Array => element.EnumerateArray().Select(FromJsonElement).ToList(),
            _ => element.Clone(),
        };
}
