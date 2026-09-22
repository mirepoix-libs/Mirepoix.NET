using System.Net;
using System.Text;
using System.Text.Json;
using Mirepoix.AccessControl.Protocol.Http;
using Mirepoix.AccessControl.Providers;
using Mirepoix.AccessControl.Providers.Client.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Providers.Client.Http.Tests;

public sealed class HttpSubjectResolverTests
{
    [Fact]
    public async Task HttpSubjectResolver_PostsPartial_AndMapsSubject()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                AccessControlHttpRoutes.AbsoluteProvidersSubjectHydratePath,
                request.RequestUri!.AbsolutePath);

            var body = SubjectDto.FromDomain(new Subject(
                "alice",
                new HashSet<string> { "editor" },
                new Dictionary<string, object?> { ["dept"] = "eng" }));
            return JsonResponse(body);
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var resolver = new HttpSubjectResolver(http);

        var result = await resolver.HydrateAsync(
            new Subject("alice", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.Equal("alice", result.Id);
        Assert.Contains("editor", result.Roles);
        Assert.Equal("eng", result.Attributes["dept"]);
    }

    [Fact]
    public void AddAccessControlProvidersClientHttp_Throws_WhenBaseAddressMissing()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddAccessControlProvidersClientHttp(options => options.AddSubject()));

        Assert.Contains("BaseAddress", exception.Message);
    }

    [Fact]
    public void AddAccessControlProvidersClientHttp_Throws_WhenNoSlices()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddAccessControlProvidersClientHttp(options =>
            {
                options.BaseAddress = new Uri("http://localhost/");
            }));

        Assert.Contains("slice", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddAccessControlProvidersClientHttp_RegistersHttpSubjectResolver()
    {
        var services = new ServiceCollection();
        services.AddAccessControlProvidersClientHttp(options =>
        {
            options.BaseAddress = new Uri("http://localhost/");
            options.AddSubject();
        });

        using var provider = services.BuildServiceProvider();
        Assert.IsType<HttpSubjectResolver>(provider.GetRequiredService<ISubjectResolver>());
        Assert.IsType<CompositeBundleHydrator>(provider.GetRequiredService<IBundleHydrator>());
    }

    private static HttpResponseMessage JsonResponse<T>(T body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, AccessControlHttpJson.DefaultOptions),
                Encoding.UTF8,
                "application/json"),
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}
