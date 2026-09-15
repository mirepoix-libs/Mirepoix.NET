using System.Text.Json;
using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Providers.Codec;

public static class PolicySetStorageCodec
{
    public static string ToStorageJson(PolicySet set) => PolicySerializers.ToJson(set);

    public static PolicySet FromStorageJson(string json) => PolicySerializers.FromJson(json);
}

public static class AttributeValueCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static string? ToJson(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, JsonOptions);

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
