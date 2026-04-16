using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;

namespace Scaffold.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/connections")]
public class ConnectionController : ControllerBase
{
    private readonly IAssessmentEngineFactory _assessmentEngineFactory;

    public ConnectionController(IAssessmentEngineFactory assessmentEngineFactory)
    {
        _assessmentEngineFactory = assessmentEngineFactory;
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
            Platform = request.Platform,
        };

        var assessmentEngine = _assessmentEngineFactory.Create(connectionInfo.Platform);
        var success = await assessmentEngine.TestConnectionAsync(connectionInfo);
        return Ok(new { success });
    }
}

public record ConnectionTestRequest(
    [Required] string Server,
    [Required] string Database,
    int Port = 1433,
    bool UseSqlAuthentication = false,
    string? Username = null,
    string? Password = null,
    string? KeyVaultSecretUri = null,
    bool TrustServerCertificate = false,
    DatabasePlatform Platform = DatabasePlatform.SqlServer);
