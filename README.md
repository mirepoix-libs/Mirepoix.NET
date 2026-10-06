<p align="center">
  <img src="img/mirepoix-logo.png" alt="Mirepoix" width="280"/>
</p>

# Mirepoix.AccessControl

Authorization libraries for .NET 8. One attribute-based evaluation kernel evaluates policies as data and returns Allow or Deny.

Target framework: `net8.0`. License: [MIT](LICENSE).

Public member contracts ship as XML documentation with each package.

## Packages

These sixteen packages are the ones [`.github/workflows/publish.yml`](.github/workflows/publish.yml) packs and pushes to NuGet.

| Package | Role |
|---|---|
| `Mirepoix.AccessControl` | Shared request and decision types: `AuthorizationRequest`, `AccessDecision`, `Subject`, `Resource`, `IAccessChecker` |
| `Mirepoix.AccessControl.Core` | Policy language and the PIP/PAP seams |
| `Mirepoix.AccessControl.Engine` | PDP: `Authorizer`, combination strategies, `LocalAccessChecker`, and the in-memory policy source |
| `Mirepoix.AccessControl.Engine.DependencyInjection` | Hand-roll DI: `AddAccessControlResourceResolver<T>()` |
| `Mirepoix.AccessControl.Authorization.AspNetCore` | ASP.NET Core PEP |
| `Mirepoix.AccessControl.Providers` | Shared PIP contract (schema and codecs) |
| `Mirepoix.AccessControl.Providers.SqlServer` | SqlServer read/hydrate PIP (`Microsoft.Data.SqlClient`) |
| `Mirepoix.AccessControl.Providers.EntityFrameworkCore` | Entity Framework Core read/hydrate PIP |
| `Mirepoix.AccessControl.Management` | Operation catalog: pull enforcement catalogs and decorate `IPolicySetEditor` |
| `Mirepoix.AccessControl.Management.EntityFrameworkCore` | Entity Framework Core PAP writers |
| `Mirepoix.AccessControl.Protocol.Http` | HTTP wire contracts |
| `Mirepoix.AccessControl.Engine.Client.Http` | Remote-eval client: `IAccessChecker` over HTTP |
| `Mirepoix.AccessControl.Engine.Server.Http` | PDP HTTP server |
| `Mirepoix.AccessControl.Providers.Client.Http` | Remote PIP client |
| `Mirepoix.AccessControl.Providers.Server.Http` | PIP HTTP server |
| `Mirepoix.AccessControl.Management.Server.Http` | PAP HTTP server |

## Install

In-process enforcement:

```bash
dotnet add package Mirepoix.AccessControl.Authorization.AspNetCore
```

SqlServer policy source (call `AddAccessControlProviders` before `AddAccessControl`):

```bash
dotnet add package Mirepoix.AccessControl.Providers.SqlServer
```

## Quick start

Memory policy set:

```csharp
services.AddAccessControl(ac =>
{
    ac.UseMemoryPolicySet(policySet);
    ac.UseCompositeHydrator(subject, resource, context); // optional
});
```

SqlServer:

```csharp
services.AddAccessControlProviders(o => o.ConnectionString = cs);
services.AddAccessControl();
```

Extension methods are `AddAccessControl` and `RequireAccessControl`. Package and namespace names use `Mirepoix.AccessControl.*`.

## Build and test

Requires the .NET 8 SDK.

```bash
dotnet test Mirepoix.NET.sln
```

## Publish

A tag named `prerelease/v*` or `release/v*` runs [`.github/workflows/publish.yml`](.github/workflows/publish.yml). The workflow tests every `*.Tests.csproj`, packs the packages above at the version in the tag, and pushes them to nuget.org.

## License

[MIT](LICENSE)
