using System.Text.Json;
using Mirepoix.AccessControl;

public class PublishedOperationTests
{
    [Fact]
    public void Serializes_camel_case()
    {
        var json = JsonSerializer.Serialize(
            new PublishedOperation("invoice:post", "invoices/{id}", "POST"),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Contains("\"operation\":\"invoice:post\"", json);
        Assert.Contains("\"routeTemplate\":\"invoices/{id}\"", json);
        Assert.Contains("\"httpMethod\":\"POST\"", json);
        Assert.Equal("/access-control/operations", PublishedOperation.EnforcementPath);
    }
}
