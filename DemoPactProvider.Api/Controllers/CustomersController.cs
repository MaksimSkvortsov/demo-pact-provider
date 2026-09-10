using DemoPactProvider.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace DemoPactProvider.Api.Controllers;

[ApiController]
[Route("customers")]
public sealed class CustomersController : ControllerBase
{
    [HttpGet("{id:int}")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    public ActionResult<CustomerResponse> Get(int id)
    {
        return Ok(new CustomerResponse(
            Id: id,
            Email: "john@example.com",
            Phone: "555-1234"));
    }
}
