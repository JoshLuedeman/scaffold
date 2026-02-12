using Moq;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;
using Scaffold.Migration.SqlServer;

namespace Scaffold.Migration.Tests;

public class ScriptExecutorIntegrationTests
{
    [Fact]
    public async Task ExecuteCutover_CallsScriptsInCorrectOrder()
    {
        var callOrder = new List<string>();

        var schemaDeployer = new Mock<SchemaDeployer>();
        schemaDeployer.Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => callOrder.Add("schema"));

        var bulkCopier = new Mock<BulkDataCopier>();
        bulkCopier.Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100L)
            .Callback(() => callOrder.Add("data"));

        var scriptExecutor = new Mock<ScriptExecutor>();
        scriptExecutor.Setup(s => s.ExecuteScriptsAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<MigrationScript>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<string, IReadOnlyList<MigrationScript>, IProgress<MigrationProgress>?, CancellationToken>(
                (_, scripts, _, _) => callOrder.Add(scripts[0].Phase == MigrationScriptPhase.Pre ? "pre-scripts" : "post-scripts"));

        var migrator = new SqlServerMigrator(schemaDeployer.Object, bulkCopier.Object, scriptExecutor.Object);

        var plan = new MigrationPlan
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Strategy = MigrationStrategy.Cutover,
            SourceConnectionString = "Server=src;Database=db;User Id=sa;Password=pass;",
            ExistingTargetConnectionString = "Server=tgt;Database=db;User Id=sa;Password=pass;",
            PreMigrationScripts = [new MigrationScript { ScriptId = "pre1", Label = "Pre", Phase = MigrationScriptPhase.Pre, SqlContent = "SELECT 1", IsEnabled = true, Order = 0 }],
            PostMigrationScripts = [new MigrationScript { ScriptId = "post1", Label = "Post", Phase = MigrationScriptPhase.Post, SqlContent = "SELECT 1", IsEnabled = true, Order = 0 }]
        };

        var result = await migrator.ExecuteCutoverAsync(plan);

        Assert.True(result.Success);
        Assert.Equal(["schema", "pre-scripts", "data", "post-scripts"], callOrder);
    }

    [Fact]
    public async Task ExecuteCutover_NoScripts_SkipsScriptExecution()
    {
        var schemaDeployer = new Mock<SchemaDeployer>();
        schemaDeployer.Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var bulkCopier = new Mock<BulkDataCopier>();
        bulkCopier.Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(50L);

        var scriptExecutor = new Mock<ScriptExecutor>();

        var migrator = new SqlServerMigrator(schemaDeployer.Object, bulkCopier.Object, scriptExecutor.Object);

        var plan = new MigrationPlan
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Strategy = MigrationStrategy.Cutover,
            SourceConnectionString = "Server=src;Database=db;User Id=sa;Password=pass;",
            ExistingTargetConnectionString = "Server=tgt;Database=db;User Id=sa;Password=pass;"
        };

        var result = await migrator.ExecuteCutoverAsync(plan);

        Assert.True(result.Success);
        Assert.Equal(50, result.RowsMigrated);
        scriptExecutor.Verify(s => s.ExecuteScriptsAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<MigrationScript>>(),
            It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteCutover_ScriptFailure_FailsMigration()
    {
        var schemaDeployer = new Mock<SchemaDeployer>();
        schemaDeployer.Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scriptExecutor = new Mock<ScriptExecutor>();
        scriptExecutor.Setup(s => s.ExecuteScriptsAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<MigrationScript>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Script execution failed"));

        var bulkCopier = new Mock<BulkDataCopier>();

        var migrator = new SqlServerMigrator(schemaDeployer.Object, bulkCopier.Object, scriptExecutor.Object);

        var plan = new MigrationPlan
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Strategy = MigrationStrategy.Cutover,
            SourceConnectionString = "Server=src;Database=db;User Id=sa;Password=pass;",
            ExistingTargetConnectionString = "Server=tgt;Database=db;User Id=sa;Password=pass;",
            PreMigrationScripts = [new MigrationScript { ScriptId = "pre1", Label = "Bad Script", Phase = MigrationScriptPhase.Pre, SqlContent = "INVALID SQL", IsEnabled = true, Order = 0 }]
        };

        var result = await migrator.ExecuteCutoverAsync(plan);

        Assert.False(result.Success);
        Assert.Contains("Script execution failed", result.Errors);
    }
}
