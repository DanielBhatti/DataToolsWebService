using Microsoft.AspNetCore.Mvc;

namespace DataToolsWebService.Controllers;

[ApiController]
[Route("")]
public sealed class RootController : ControllerBase
{
    [HttpGet("ping")]
    public ActionResult<string> Ping()
    {
        return Ok("Pong");
    }
}
