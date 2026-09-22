using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mirepoix.AccessControl.Protocol.Http;

/// <summary>
/// Shared JSON serializer options and attribute-bag conversion helpers for HTTP wire DTOs.
/// </summary>
public static class AccessControlHttpJson
{
    /// <summary>
    /// Default wire options: camelCase property names and string enum serialization.
    /// </summary>
    public static JsonSerializerOptions DefaultOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Converts a JSON element to a CLR value using Management-compatible rules.</summary>
    internal static object? ToClrValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => value.TryGetInt32(out var integer) ? integer
                : value.TryGetInt64(out var longInteger) ? longInteger
                : value.GetDouble(),
            JsonValueKind.Array => value.EnumerateArray().Select(ToClrValue).ToArray(),
            JsonValueKind.Object => value.EnumerateObject()
                .ToDictionary(property => property.Name, property => ToClrValue(property.Value)),
            _ => value.Clone(),
        };

    /// <summary>Converts a CLR attribute bag to JSON elements for wire serialization.</summary>
    internal static Dictionary<string, JsonElement>? ToJsonElements(IReadOnlyDictionary<string, object?>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return attributes is null ? null : new Dictionary<string, JsonElement>();
        }

        return attributes.ToDictionary(
            pair => pair.Key,
            pair => JsonSerializer.SerializeToElement(pair.Value, DefaultOptions));
    }

    /// <summary>Converts a JSON attribute bag to a CLR dictionary for domain mapping.</summary>
    internal static Dictionary<string, object?> ToAttributeDictionary(Dictionary<string, JsonElement>? attributes) =>
        attributes?.ToDictionary(pair => pair.Key, pair => ToClrValue(pair.Value))
        ?? new Dictionary<string, object?>();
}
