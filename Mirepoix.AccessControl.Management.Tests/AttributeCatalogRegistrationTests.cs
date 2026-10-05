using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Management;

public class AttributeCatalogRegistrationTests
{
    [Fact]
    public void AddProviderApp_and_AddLocalProviderApp_reject_a_repeated_name_in_that_list_only()
    {
        var options = new AttributeCatalogOptions();
        options.AddProviderApp("identity", "https://identity.example");
        options.AddLocalProviderApp();
        options.AddEnforcementApp("billing", "https://billing.example");
        options.AddLocalEnforcementApp();

        Assert.Equal("identity", options.ProviderApps[0].Name);
        Assert.Equal("https://identity.example", options.ProviderApps[0].Origin);
        Assert.False(options.ProviderApps[0].IsLocal);
        Assert.Equal("local", options.ProviderApps[1].Name);
        Assert.Null(options.ProviderApps[1].Origin);
        Assert.True(options.ProviderApps[1].IsLocal);
        Assert.Equal("local", options.EnforcementApps[1].Name);
        Assert.True(options.EnforcementApps[1].IsLocal);

        Assert.Throws<ArgumentException>(() => options.AddProviderApp("identity", "https://other.example"));
        Assert.Throws<ArgumentException>(() => options.AddLocalProviderApp("identity"));
        Assert.Throws<ArgumentException>(() => options.AddLocalEnforcementApp("billing"));
        Assert.Throws<ArgumentException>(() => options.AddProviderApp(" ", "https://identity.example"));
        Assert.Throws<ArgumentException>(() => options.AddProviderApp("other", " "));
        Assert.Throws<ArgumentException>(() => options.AddLocalProviderApp(" "));
    }

    [Fact]
    public void AddAccessControlAttributeCatalog_registers_named_client_hook_and_ignores_a_second_call()
    {
        var services = new ServiceCollection();
        services.AddAccessControlAttributeCatalog(options =>
        {
            options.AddProviderApp("identity", "https://identity.example");
            options.ConfigureHttpClient = client => client.DefaultRequestHeaders.Add("X-Caller", "management");
        });
        services.AddAccessControlAttributeCatalog(options =>
            options.AddProviderApp("other", "https://other.example"));

        using var provider = services.BuildServiceProvider();
        var stored = provider.GetRequiredService<AttributeCatalogOptions>();
        Assert.Equal("identity", Assert.Single(stored.ProviderApps).Name);
        Assert.IsType<AttributeCatalogSnapshot>(provider.GetRequiredService<AttributeCatalogSnapshot>());
        Assert.IsType<AttributeCatalogValidator>(provider.GetRequiredService<AttributeCatalogValidator>());
        Assert.IsType<AttributeCatalogClient>(provider.GetRequiredService<IAttributeCatalogClient>());

        var http = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(AttributeCatalogServiceCollectionExtensions.HttpClientName);
        Assert.Equal(
            "AccessControl.AttributeCatalog",
            AttributeCatalogServiceCollectionExtensions.HttpClientName);
        Assert.Equal("management", http.DefaultRequestHeaders.GetValues("X-Caller").Single());
    }

    [Fact]
    public void AddAccessControlAttributeCatalog_allows_an_empty_app_list()
    {
        var services = new ServiceCollection();
        services.AddAccessControlAttributeCatalog(_ => { });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<AttributeCatalogOptions>();
        Assert.Empty(options.ProviderApps);
        Assert.Empty(options.EnforcementApps);
    }
}
