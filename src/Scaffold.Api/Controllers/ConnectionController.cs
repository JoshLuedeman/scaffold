using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scaffold.Core.Interfaces;

namespace Scaffold.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/connections")]
public class ConnectionController : ControllerBase
{
    private readonly IAssessmentEngine _assessmentEngine;

    public ConnectionController(IAssessmentEngine assessmentEngine)
    {
        _assessmentEngine = assessmentEngine;
    }

    [HttpPost("test")]
    public async Task<IActionResult> TestConnection([FromBody] Core.Models.ConnectionInfo connectionInfo)
    {
        var success = await _assessmentEngine.TestConnectionAsync(connectionInfo);
        return Ok(new { success });
    }
}
