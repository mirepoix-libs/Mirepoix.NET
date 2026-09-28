using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mirepoix.AccessControl.Management.Server.Http.Tests;

internal static class ManagementTestCaller
{
    public const string HeaderValue = "Test ada:PolicyAdmin";

    public static void Register(IServiceCollection services, IAccessChecker checker)
    {
        services.AddSingleton(checker);
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = "Test";
            options.DefaultForbidScheme = "Test";
        }).AddScheme<AuthenticationSchemeOptions, HeaderHandler>("Test", _ => { });
    }

    public static void Use(WebApplication app) => app.UseAuthentication();

    public static HttpClient Client(WebApplication app)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", HeaderValue);
        return client;
    }

    private sealed class HeaderHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public HeaderHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var header))
                return Task.FromResult(AuthenticateResult.NoResult());

            var value = header.ToString();
            var space = value.IndexOf(' ');
            var identityValue = space >= 0 ? value[(space + 1)..] : value;
            var colon = identityValue.IndexOf(':');
            var user = colon >= 0 ? identityValue[..colon] : identityValue;
            var role = colon >= 0 ? identityValue[(colon + 1)..] : string.Empty;
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user) };
            if (role.Length > 0)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var identity = new ClaimsIdentity(claims, "Test");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
    }
}

internal sealed class ScriptedChecker : IAccessChecker
{
    public ScriptedChecker(AuthorizationResult result) => Result = result;

    public AuthorizationResult Result { get; }

    public int Calls { get; private set; }

    public string? LastOperation { get; private set; }

    public Task<AccessDecision> CheckAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        Calls++;
        LastOperation = request.Operation.Value;
        return Task.FromResult(new AccessDecision(
            Result,
            [],
            Result == AuthorizationResult.Allow ? DecisionStatus.Success : DecisionStatus.Defaulted,
            "v"));
    }
}
