using Mirepoix.AccessControl.Engine.Client.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Engine.Client.Http.Tests;

public sealed class AccessControlEngineClientHttpExtensionsTests
{
    [Fact]
    public void AddAccessControlEngineClientHttp_RegistersHttpAccessChecker()
    {
        var services = new ServiceCollection();
        services.AddAccessControlEngineClientHttp(options =>
            options.BaseAddress = new Uri("http://localhost/"));

        using var provider = services.BuildServiceProvider();
        var checker = provider.GetRequiredService<IAccessChecker>();

        Assert.IsType<HttpAccessChecker>(checker);
    }

    [Fact]
    public void AddAccessControlEngineClientHttp_Throws_WhenBaseAddressMissing()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddAccessControlEngineClientHttp(_ => { }));

        Assert.Contains("BaseAddress", exception.Message);
    }

    [Fact]
    public void AddAccessControlEngineClientHttp_Throws_WhenBaseAddressRelative()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddAccessControlEngineClientHttp(options =>
                options.BaseAddress = new Uri("pdp/", UriKind.Relative)));

        Assert.Contains("absolute", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
