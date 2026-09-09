using DemoPactProvider.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace DemoPactProvider.Api.Controllers;

[ApiController]
[Route("provider-states")]
public sealed class ProviderStatesController : ControllerBase
{
    // Pact calls this endpoint before verifying interactions that declare a
    // provider state, such as "a customer with id 123 exists". A real provider
    // could seed a database here. This demo has static data, so acknowledging
    // the state is enough.
    [AcceptVerbs("POST", "PUT")]
    public IActionResult SetProviderState([FromBody] ProviderStateRequest? request)
    {
        return Ok();
    }
}
