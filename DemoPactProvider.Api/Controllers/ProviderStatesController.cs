using DemoPactProvider.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace DemoPactProvider.Api.Controllers;

[ApiController]
[Route("provider-states")]
public sealed class ProviderStatesController : ControllerBase
{
    [AcceptVerbs("POST", "PUT")]
    public IActionResult SetProviderState([FromBody] ProviderStateRequest? request)
    {
        return Ok();
    }
}
