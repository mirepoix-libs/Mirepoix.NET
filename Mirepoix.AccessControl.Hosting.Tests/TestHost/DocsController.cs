using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mirepoix.AccessControl.Hosting;

namespace Mirepoix.AccessControl.Hosting.Tests.TestHost;

[ApiController]
[Route("mvc/docs")]
[Authorize(Policy = AccessControlOptions.PolicyName)]
public sealed class DocsController : ControllerBase
{
    [HttpGet("{id}")]
    [AccessOperation("doc:edit")]
    [AccessResource("doc")]
    public IActionResult Get(string id) => Content(id);
}
