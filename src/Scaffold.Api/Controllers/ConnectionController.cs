using System.ComponentModel.DataAnnotations;
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
    public async Task<IActionResult> TestConnection([FromBody] ConnectionTestRequest request)
    {
        var connectionInfo = new Core.Models.ConnectionInfo
        {
            Server = request.Server,
            Database = request.Database,
            Port = request.Port,
            UseSqlAuthentication = request.UseSqlAuthentication,
            Username = request.Username,
            Password = request.Password,
            KeyVaultSecretUri = request.KeyVaultSecretUri,
            TrustServerCertificate = request.TrustServerCertificate,
        };

        var success = await _assessmentEngine.TestConnectionAsync(connectionInfo);
        return Ok(new { success });
    }
}

public record ConnectionTestRequest(
    [Required][StringLength(500)] string Server,
    [Required][StringLength(200)] string Database,
    [Range(1, 65535)] int Port = 1433,
    bool UseSqlAuthentication = false,
    [StringLength(200)] string? Username = null,
    string? Password = null,
    [StringLength(500)] string? KeyVaultSecretUri = null,
    bool TrustServerCertificate = false);
