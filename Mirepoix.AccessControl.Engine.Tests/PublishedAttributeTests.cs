using System.Text.Json;
using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Policy;

public class PublishedAttributeTests
{
    [Fact]
    public void Serializes_camel_case_and_omits_null_type()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var withType = JsonSerializer.Serialize(
            new PublishedAttribute(AttributeTarget.Subject, "subject", "dept"), options);
        var context = JsonSerializer.Serialize(
            new PublishedAttribute(AttributeTarget.Context, null, "time"), options);

        Assert.Contains("\"target\":\"subject\"", withType);
        Assert.Contains("\"type\":\"subject\"", withType);
        Assert.Contains("\"key\":\"dept\"", withType);
        Assert.Contains("\"key\":\"time\"", context);
        Assert.DoesNotContain("\"type\":", context); // null omitted, or assert null if PreferNull
        Assert.Equal("/access-control/attributes", PublishedAttribute.CatalogPath);
    }
}
