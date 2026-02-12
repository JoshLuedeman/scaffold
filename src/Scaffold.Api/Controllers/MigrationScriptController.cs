using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scaffold.Assessment.SqlServer;
using Scaffold.Core.Interfaces;

namespace Scaffold.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId:guid}/migration-scripts")]
public class MigrationScriptController : ControllerBase
{
    private readonly IProjectRepository _projectRepository;

    public MigrationScriptController(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    /// <summary>Lists available canned scripts with object counts from the assessment.</summary>
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableScripts(Guid projectId)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            
            if (project.Assessment?.Schema is null)
                return NotFound("No assessment found. Run an assessment first.");

            var scripts = MigrationScriptGenerator.GetAvailableScripts(project.Assessment.Schema);
            return Ok(scripts);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Generates a preview of the SQL for a canned script.</summary>
    [HttpGet("preview")]
    public async Task<IActionResult> PreviewScript(Guid projectId, [FromQuery] string scriptId)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            
            if (project.Assessment?.Schema is null)
                return NotFound("No assessment found. Run an assessment first.");

            if (string.IsNullOrEmpty(scriptId))
                return BadRequest("scriptId query parameter is required.");

            var sql = MigrationScriptGenerator.Generate(scriptId, project.Assessment.Schema);
            
            if (sql is null)
                return NotFound($"Unknown script ID: {scriptId}");

            return Ok(new { scriptId, sql });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
