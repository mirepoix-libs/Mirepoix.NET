using System.Net;
using System.Net.Http.Headers;
using Mirepoix.AccessControl.Hosting.Tests.TestHost;

namespace Mirepoix.AccessControl.Hosting.Tests;

public class AccessControlWebIntegrationTests : IClassFixture<AccessControlApiFactory>
{
    private readonly AccessControlApiFactory _factory;

    public AccessControlWebIntegrationTests(AccessControlApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/mvc/docs/42")]
    [InlineData("/minimal/docs/42")]
    [InlineData("/filter/docs/42")]
    public async Task Allowed_editor_gets_200(string path)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName, "u1:EDITOR");

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("42", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/mvc/docs/42")]
    [InlineData("/minimal/docs/42")]
    [InlineData("/filter/docs/42")]
    public async Task Wrong_role_gets_403(string path)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName, "u1:USER");

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/mvc/docs/42")]
    [InlineData("/minimal/docs/42")]
    [InlineData("/filter/docs/42")]
    public async Task Unauthenticated_gets_401(string path)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Missing_operation_metadata_gets_403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName, "u1:EDITOR");

        var response = await client.GetAsync("/minimal/no-op");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
