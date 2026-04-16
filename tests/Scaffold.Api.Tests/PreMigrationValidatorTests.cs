using Scaffold.Api.Services;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Api.Tests;

public class PreMigrationValidatorTests
{
    private readonly PreMigrationValidator _validator = new();

    private static MigrationPlan CreateValidPlan() => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        Strategy = MigrationStrategy.Cutover,
        IncludedObjects = ["dbo.Users", "dbo.Orders"],
        SourceConnectionString = "Server=localhost;Database=Source;TrustServerCertificate=True",
        ScheduledAt = DateTime.UtcNow.AddHours(1),
        PreMigrationScripts = [new MigrationScript { ScriptId = "pre-1", Label = "pre.sql" }],
        PostMigrationScripts = [new MigrationScript { ScriptId = "post-1", Label = "post.sql" }]
    };

    [Fact]
    public async Task ValidateAsync_ValidPlan_ReturnsValid()
    {
        var plan = CreateValidPlan();
        var result = await _validator.ValidateAsync(plan);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ValidateAsync_EmptyIncludedObjects_ReturnsError()
    {
        var plan = CreateValidPlan();
        plan.IncludedObjects = [];
        var result = await _validator.ValidateAsync(plan);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("IncludedObjects"));
    }

    [Fact]
    public async Task ValidateAsync_ScheduledAtInPast_ReturnsError()
    {
        var plan = CreateValidPlan();
        plan.ScheduledAt = DateTime.UtcNow.AddHours(-1);
        var result = await _validator.ValidateAsync(plan);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ScheduledAt"));
    }

    [Fact]
    public async Task ValidateAsync_NullScheduledAt_DoesNotReturnError()
    {
        var plan = CreateValidPlan();
        plan.ScheduledAt = null;
        var result = await _validator.ValidateAsync(plan);
        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Errors, e => e.Contains("ScheduledAt"));
    }

    [Fact]
    public async Task ValidateAsync_MissingSourceConnectionString_ReturnsError()
    {
        var plan = CreateValidPlan();
        plan.SourceConnectionString = null;
        var result = await _validator.ValidateAsync(plan);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("SourceConnectionString"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_WhitespaceSourceConnectionString_ReturnsError(string connectionString)
    {
        var plan = CreateValidPlan();
        plan.SourceConnectionString = connectionString;
        var result = await _validator.ValidateAsync(plan);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("SourceConnectionString"));
    }

    [Fact]
    public async Task ValidateAsync_InvalidStrategy_ReturnsError()
    {
        var plan = CreateValidPlan();
        plan.Strategy = (MigrationStrategy)999;
        var result = await _validator.ValidateAsync(plan);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("strategy"));
    }

    [Fact]
    public async Task ValidateAsync_MissingPreMigrationScripts_ReturnsWarning()
    {
        var plan = CreateValidPlan();
        plan.PreMigrationScripts = [];
        var result = await _validator.ValidateAsync(plan);
        Assert.True(result.IsValid, "Missing scripts should be a warning, not an error");
        Assert.Contains(result.Warnings, w => w.Contains("pre-migration"));
    }

    [Fact]
    public async Task ValidateAsync_MissingPostMigrationScripts_ReturnsWarning()
    {
        var plan = CreateValidPlan();
        plan.PostMigrationScripts = [];
        var result = await _validator.ValidateAsync(plan);
        Assert.True(result.IsValid, "Missing scripts should be a warning, not an error");
        Assert.Contains(result.Warnings, w => w.Contains("post-migration"));
    }

    [Fact]
    public async Task ValidateAsync_MultipleErrors_ReturnsAllErrors()
    {
        var plan = CreateValidPlan();
        plan.IncludedObjects = [];
        plan.SourceConnectionString = null;
        plan.ScheduledAt = DateTime.UtcNow.AddHours(-1);
        var result = await _validator.ValidateAsync(plan);
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 3, "Expected at least 3 errors but got " + result.Errors.Count);
        Assert.Contains(result.Errors, e => e.Contains("IncludedObjects"));
        Assert.Contains(result.Errors, e => e.Contains("SourceConnectionString"));
        Assert.Contains(result.Errors, e => e.Contains("ScheduledAt"));
    }

    [Fact]
    public async Task ValidateAsync_MissingBothScripts_ReturnsBothWarnings()
    {
        var plan = CreateValidPlan();
        plan.PreMigrationScripts = [];
        plan.PostMigrationScripts = [];
        var result = await _validator.ValidateAsync(plan);
        Assert.True(result.IsValid, "Missing scripts should be warnings, not errors");
        Assert.Equal(2, result.Warnings.Count);
        Assert.Contains(result.Warnings, w => w.Contains("pre-migration"));
        Assert.Contains(result.Warnings, w => w.Contains("post-migration"));
    }

    [Fact]
    public void IsValid_NoErrors_ReturnsTrue()
    {
        var result = new PreMigrationValidationResult();
        Assert.True(result.IsValid);
    }

    [Fact]
    public void IsValid_WithErrors_ReturnsFalse()
    {
        var result = new PreMigrationValidationResult();
        result.Errors.Add("Some error");
        Assert.False(result.IsValid);
    }
}
