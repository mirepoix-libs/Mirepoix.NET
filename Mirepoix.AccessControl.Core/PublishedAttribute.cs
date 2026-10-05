namespace Mirepoix.AccessControl;

using System.Text.Json;
using System.Text.Json.Serialization;
using Mirepoix.AccessControl.Policy;

/// <summary>One attribute key a provider or enforcement app publishes.</summary>
/// <param name="Target">Subject, resource, or context.</param>
/// <param name="Type">Required for subject/resource; null for context.</param>
/// <param name="Key">Bundle attribute key.</param>
public sealed record PublishedAttribute(
    [property: JsonConverter(typeof(CamelCaseAttributeTargetConverter))] AttributeTarget Target,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Type,
    string Key)
{
    /// <summary>Relative path of the attribute catalog GET on provider and enforcement hosts.</summary>
    public const string CatalogPath = "/access-control/attributes";
}

file sealed class CamelCaseAttributeTargetConverter : JsonStringEnumConverter
{
    public CamelCaseAttributeTargetConverter() : base(JsonNamingPolicy.CamelCase) { }
}
