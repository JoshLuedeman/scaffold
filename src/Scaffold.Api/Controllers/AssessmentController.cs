using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Infrastructure.Data;

namespace Scaffold.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId:guid}/assessments")]
public class AssessmentController : ControllerBase
{
    private readonly IProjectRepository _projectRepository;
    private readonly IAssessmentEngine _assessmentEngine;
    private readonly ScaffoldDbContext _dbContext;

    public AssessmentController(
        IProjectRepository projectRepository,
        IAssessmentEngine assessmentEngine,
        ScaffoldDbContext dbContext)
    {
        _projectRepository = projectRepository;
        _assessmentEngine = assessmentEngine;
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<IActionResult> StartAssessment(Guid projectId, CancellationToken ct)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);

            if (project.SourceConnection is null)
                return BadRequest("Project has no source connection configured.");

            project.Status = ProjectStatus.Assessing;
            await _projectRepository.UpdateAsync(project);

            var report = await _assessmentEngine.AssessAsync(project.SourceConnection, ct);
            report.ProjectId = projectId;

            // Replace existing assessment if present
            if (project.Assessment is not null)
            {
                _dbContext.AssessmentReports.Remove(project.Assessment);
            }

            _dbContext.AssessmentReports.Add(report);
            await _dbContext.SaveChangesAsync(ct);

            project.Assessment = report;
            project.Status = ProjectStatus.Assessed;
            await _projectRepository.UpdateAsync(project);

            return Ok(report);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest(Guid projectId)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);

            if (project.Assessment is null)
                return NotFound("No assessment found for this project.");

            return Ok(project.Assessment);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
