using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scaffold.Assessment.SqlServer;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;
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
    public async Task<IActionResult> StartAssessment(
        Guid projectId,
        [FromBody] AssessmentRequest? request,
        CancellationToken ct)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);

            // If project has no source connection, create one from the request
            if (project.SourceConnection is null && request is not null)
            {
                var newConnection = new Scaffold.Core.Models.ConnectionInfo
                {
                    Id = Guid.NewGuid(),
                    Server = request.Server ?? "",
                    Database = request.Database ?? "",
                    Port = request.Port ?? 1433,
                    UseSqlAuthentication = request.UseSqlAuthentication ?? false,
                    Username = request.Username,
                    TrustServerCertificate = request.TrustServerCertificate ?? false,
                    Password = request.Password
                };
                project.SourceConnection = newConnection;
                _dbContext.ConnectionInfos.Add(newConnection);
                await _dbContext.SaveChangesAsync(ct);
            }

            if (project.SourceConnection is null)
                return BadRequest("Provide connection details in the request body or configure a source connection on the project first.");

            // Merge runtime credentials into the stored connection info
            var connectionInfo = project.SourceConnection;
            if (request is not null)
            {
                if (!string.IsNullOrEmpty(request.Server))
                    connectionInfo.Server = request.Server;
                if (!string.IsNullOrEmpty(request.Database))
                    connectionInfo.Database = request.Database;
                if (request.Port.HasValue)
                    connectionInfo.Port = request.Port.Value;
                if (request.UseSqlAuthentication.HasValue)
                    connectionInfo.UseSqlAuthentication = request.UseSqlAuthentication.Value;
                if (!string.IsNullOrEmpty(request.Username))
                    connectionInfo.Username = request.Username;
                if (!string.IsNullOrEmpty(request.Password))
                    connectionInfo.Password = request.Password;
                connectionInfo.TrustServerCertificate =
                    request.TrustServerCertificate ?? connectionInfo.TrustServerCertificate;
            }

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

    [HttpPost("evaluate-target")]
    public async Task<IActionResult> EvaluateTarget(
        Guid projectId,
        [FromBody] EvaluateTargetRequest request,
        CancellationToken ct)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);

            if (project.Assessment is null)
                return NotFound("No assessment found for this project. Run an assessment first.");

            var issues = CloneIssues(project.Assessment.CompatibilityIssues);
            CompatibilityChecker.ApplyTargetSeverity(issues, request.TargetService);
            var score = CompatibilityChecker.CalculateCompatibilityScore(issues);
            var risk = CompatibilityChecker.DetermineRisk(issues, score);

            return Ok(new
            {
                project.Assessment.Id,
                project.Assessment.GeneratedAt,
                TargetService = request.TargetService,
                CompatibilityIssues = issues,
                CompatibilityScore = score,
                Risk = risk
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("compatibility-summary")]
    public async Task<IActionResult> GetCompatibilitySummary(Guid projectId)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(projectId);

            if (project.Assessment is null)
                return NotFound("No assessment found for this project. Run an assessment first.");

            string[] services =
            [
                "Azure SQL Database",
                "Azure SQL Database Hyperscale",
                "Azure SQL Managed Instance",
                "SQL Server on Azure VM"
            ];

            var summaries = services.Select(service =>
            {
                var issues = CloneIssues(project.Assessment.CompatibilityIssues);
                CompatibilityChecker.ApplyTargetSeverity(issues, service);
                var score = CompatibilityChecker.CalculateCompatibilityScore(issues);
                var risk = CompatibilityChecker.DetermineRisk(issues, score);
                var unsupported = issues.Count(i => i.Severity == Core.Enums.CompatibilitySeverity.Unsupported);
                var partial = issues.Count(i => i.Severity == Core.Enums.CompatibilitySeverity.Partial);
                var supported = issues.Count(i => i.Severity == Core.Enums.CompatibilitySeverity.Supported);

                return new
                {
                    Service = service,
                    CompatibilityScore = score,
                    Risk = risk,
                    UnsupportedCount = unsupported,
                    PartialCount = partial,
                    SupportedCount = supported,
                    TotalIssues = issues.Count
                };
            }).ToList();

            return Ok(summaries);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private static List<CompatibilityIssue> CloneIssues(List<CompatibilityIssue> source)
    {
        return source.Select(i => new CompatibilityIssue
        {
            ObjectName = i.ObjectName,
            IssueType = i.IssueType,
            Description = i.Description,
            IsBlocking = i.IsBlocking,
            Severity = i.Severity,
            DocUrl = i.DocUrl
        }).ToList();
    }
}

public record AssessmentRequest(
    string? Server,
    string? Database,
    int? Port,
    bool? UseSqlAuthentication,
    string? Username,
    string? Password,
    bool? TrustServerCertificate,
    string? TargetService);

public record EvaluateTargetRequest(string TargetService);
