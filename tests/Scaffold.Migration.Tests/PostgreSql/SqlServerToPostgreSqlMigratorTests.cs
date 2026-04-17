using Moq;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;
using Scaffold.Migration.PostgreSql;
using Scaffold.Migration.SqlServer;
using Scaffold.Migration.Tests;

namespace Scaffold.Migration.Tests.PostgreSql;

public class SqlServerToPostgreSqlMigratorTests
{
    private static MigrationPlan CreateValidPlan(params string[] tables) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        SourceConnectionString = "Server=source;Database=TestDb;Encrypt=false;",
        ExistingTargetConnectionString = "Host=pghost;Database=testdb;Username=postgres;",
        SourcePlatform = DatabasePlatform.SqlServer,
        TargetPlatform = DatabasePlatform.PostgreSql,
        IncludedObjects = tables.ToList()
    };

    #region ExecuteCutoverAsync — schema → pre-scripts → data → post-scripts → validation flow

    [Fact]
    public async Task ExecuteCutoverAsync_CallsComponentsInCorrectOrder()
    {
        // Arrange
        var schemaDeployer = new Mock<PostgreSqlSchemaDeployer>(
            new SqlServerSchemaReader(), new DdlTranslator());
        var bulkCopier = new Mock<CrossPlatformBulkCopier>();
        var scriptExecutor = new Mock<PostgreSqlScriptExecutor>();
        var validationEngine = new Mock<PostgreSqlValidationEngine>();
        var callOrder = new List<string>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("schema"))
            .Returns(Task.CompletedTask);

        scriptExecutor
            .Setup(s => s.ExecuteScriptsAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<MigrationScript>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .Callback<string, IReadOnlyList<MigrationScript>, IProgress<MigrationProgress>?, CancellationToken, int?>(
                (_, scripts, _, _, _) =>
                {
                    var phase = scripts[0].Phase == MigrationScriptPhase.Pre ? "pre-scripts" : "post-scripts";
                    callOrder.Add(phase);
                })
            .Returns(Task.CompletedTask);

        bulkCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("data"))
            .ReturnsAsync(100L);

        validationEngine
            .Setup(v => v.ValidateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("validation"))
            .ReturnsAsync(new ValidationSummary { AllPassed = true, Results = [] });

        var migrator = new SqlServerToPostgreSqlMigrator(
            schemaDeployer.Object, bulkCopier.Object,
            scriptExecutor.Object, validationEngine.Object);

        var plan = CreateValidPlan("dbo.Users");
        plan.PreMigrationScripts =
        [
            new MigrationScript { ScriptId = "pre1", Label = "Pre", Phase = MigrationScriptPhase.Pre, SqlContent = "SELECT 1", IsEnabled = true, Order = 0 }
        ];
        plan.PostMigrationScripts =
        [
            new MigrationScript { ScriptId = "post1", Label = "Post", Phase = MigrationScriptPhase.Post, SqlContent = "SELECT 1", IsEnabled = true, Order = 0 }
        ];

        // Act
        var result = await migrator.ExecuteCutoverAsync(plan);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(new List<string> { "schema", "pre-scripts", "data", "post-scripts", "validation" }, callOrder);
    }

    [Fact]
    public async Task ExecuteCutoverAsync_NoScripts_SkipsScriptExecution()
    {
        // Arrange
        var schemaDeployer = new Mock<PostgreSqlSchemaDeployer>(
            new SqlServerSchemaReader(), new DdlTranslator());
        var bulkCopier = new Mock<CrossPlatformBulkCopier>();
        var scriptExecutor = new Mock<PostgreSqlScriptExecutor>();
        var validationEngine = new Mock<PostgreSqlValidationEngine>();
        var callOrder = new List<string>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("schema"))
            .Returns(Task.CompletedTask);

        bulkCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("data"))
            .ReturnsAsync(42L);

        validationEngine
            .Setup(v => v.ValidateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("validation"))
            .ReturnsAsync(new ValidationSummary { AllPassed = true, Results = [] });

        var migrator = new SqlServerToPostgreSqlMigrator(
            schemaDeployer.Object, bulkCopier.Object,
            scriptExecutor.Object, validationEngine.Object);

        var plan = CreateValidPlan("dbo.Users");

        // Act
        var result = await migrator.ExecuteCutoverAsync(plan);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(42, result.RowsMigrated);
        Assert.Equal(new List<string> { "schema", "data", "validation" }, callOrder);
        scriptExecutor.Verify(s => s.ExecuteScriptsAsync(
            It.IsAny<string>(), It.IsAny<IReadOnlyList<MigrationScript>>(),
            It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>(),
            It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteCutoverAsync_ReportsProgressAtEachPhase()
    {
        // Arrange
        var schemaDeployer = new Mock<PostgreSqlSchemaDeployer>(
            new SqlServerSchemaReader(), new DdlTranslator());
        var bulkCopier = new Mock<CrossPlatformBulkCopier>();
        var scriptExecutor = new Mock<PostgreSqlScriptExecutor>();
        var validationEngine = new Mock<PostgreSqlValidationEngine>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        bulkCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0L);

        validationEngine
            .Setup(v => v.ValidateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationSummary { AllPassed = true, Results = [] });

        var migrator = new SqlServerToPostgreSqlMigrator(
            schemaDeployer.Object, bulkCopier.Object,
            scriptExecutor.Object, validationEngine.Object);

        var plan = CreateValidPlan("dbo.Users");
        var phases = new List<string>();
        var progress = new SynchronousProgress<MigrationProgress>(p => phases.Add(p.Phase));

        // Act
        await migrator.ExecuteCutoverAsync(plan, progress);

        // Assert — orchestrator reports SchemaDeployment, DataMigration, and Validation phases
        Assert.Contains("SchemaDeployment", phases);
        Assert.Contains("DataMigration", phases);
        Assert.Contains("Validation", phases);
    }

    [Fact]
    public async Task ExecuteCutoverAsync_SetsResultMetadata()
    {
        // Arrange
        var schemaDeployer = new Mock<PostgreSqlSchemaDeployer>(
            new SqlServerSchemaReader(), new DdlTranslator());
        var bulkCopier = new Mock<CrossPlatformBulkCopier>();
        var scriptExecutor = new Mock<PostgreSqlScriptExecutor>();
        var validationEngine = new Mock<PostgreSqlValidationEngine>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        bulkCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(250L);

        validationEngine
            .Setup(v => v.ValidateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationSummary { AllPassed = true, Results = [] });

        var migrator = new SqlServerToPostgreSqlMigrator(
            schemaDeployer.Object, bulkCopier.Object,
            scriptExecutor.Object, validationEngine.Object);

        var plan = CreateValidPlan("dbo.Users");

        // Act
        var result = await migrator.ExecuteCutoverAsync(plan);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(plan.ProjectId, result.ProjectId);
        Assert.True(result.Success);
        Assert.Equal(250, result.RowsMigrated);
        Assert.NotNull(result.CompletedAt);
        Assert.True(result.StartedAt <= result.CompletedAt);
    }

    [Fact]
    public async Task ExecuteCutoverAsync_ValidationFailure_ResultNotSuccess()
    {
        // Arrange
        var schemaDeployer = new Mock<PostgreSqlSchemaDeployer>(
            new SqlServerSchemaReader(), new DdlTranslator());
        var bulkCopier = new Mock<CrossPlatformBulkCopier>();
        var scriptExecutor = new Mock<PostgreSqlScriptExecutor>();
        var validationEngine = new Mock<PostgreSqlValidationEngine>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        bulkCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(100L);

        validationEngine
            .Setup(v => v.ValidateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationSummary
            {
                AllPassed = false,
                Results =
                [
                    new ValidationResult
                    {
                        TableName = "dbo.Users",
                        SourceRowCount = 100,
                        TargetRowCount = 95,
                        ChecksumMatch = false
                    }
                ]
            });

        var migrator = new SqlServerToPostgreSqlMigrator(
            schemaDeployer.Object, bulkCopier.Object,
            scriptExecutor.Object, validationEngine.Object);

        var plan = CreateValidPlan("dbo.Users");

        // Act
        var result = await migrator.ExecuteCutoverAsync(plan);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(100, result.RowsMigrated);
        Assert.Single(result.Validations);
        Assert.Equal("dbo.Users", result.Validations[0].TableName);
    }

    [Fact]
    public async Task ExecuteCutoverAsync_SchemaDeployFailure_CapturesError()
    {
        // Arrange
        var schemaDeployer = new Mock<PostgreSqlSchemaDeployer>(
            new SqlServerSchemaReader(), new DdlTranslator());
        var bulkCopier = new Mock<CrossPlatformBulkCopier>();
        var scriptExecutor = new Mock<PostgreSqlScriptExecutor>();
        var validationEngine = new Mock<PostgreSqlValidationEngine>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Schema deployment failed: connection refused"));

        var migrator = new SqlServerToPostgreSqlMigrator(
            schemaDeployer.Object, bulkCopier.Object,
            scriptExecutor.Object, validationEngine.Object);

        var plan = CreateValidPlan("dbo.Users");

        // Act
        var result = await migrator.ExecuteCutoverAsync(plan);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Schema deployment failed", result.Errors.Single());
        Assert.NotNull(result.CompletedAt);
    }

    [Fact]
    public async Task ExecuteCutoverAsync_DataCopyFailure_CapturesError()
    {
        // Arrange
        var schemaDeployer = new Mock<PostgreSqlSchemaDeployer>(
            new SqlServerSchemaReader(), new DdlTranslator());
        var bulkCopier = new Mock<CrossPlatformBulkCopier>();
        var scriptExecutor = new Mock<PostgreSqlScriptExecutor>();
        var validationEngine = new Mock<PostgreSqlValidationEngine>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        bulkCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("COPY protocol error"));

        var migrator = new SqlServerToPostgreSqlMigrator(
            schemaDeployer.Object, bulkCopier.Object,
            scriptExecutor.Object, validationEngine.Object);

        var plan = CreateValidPlan("dbo.Users");

        // Act
        var result = await migrator.ExecuteCutoverAsync(plan);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("COPY protocol error", result.Errors.Single());
    }

    #endregion

    #region Missing connection strings

    [Fact]
    public async Task ExecuteCutoverAsync_MissingSourceConnectionString_ReturnsFailure()
    {
        var migrator = new SqlServerToPostgreSqlMigrator();
        var plan = new MigrationPlan
        {
            SourceConnectionString = null,
            ExistingTargetConnectionString = "Host=pghost;Database=testdb;Username=postgres;"
        };

        var result = await migrator.ExecuteCutoverAsync(plan);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("SourceConnectionString"));
    }

    [Fact]
    public async Task ExecuteCutoverAsync_MissingTargetConnectionString_ReturnsFailure()
    {
        var migrator = new SqlServerToPostgreSqlMigrator();
        var plan = new MigrationPlan
        {
            SourceConnectionString = "Server=source;Database=Test;Encrypt=false;",
            ExistingTargetConnectionString = null
        };

        var result = await migrator.ExecuteCutoverAsync(plan);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("ExistingTargetConnectionString"));
    }

    [Fact]
    public async Task ExecuteCutoverAsync_EmptySourceConnectionString_ReturnsFailure()
    {
        var migrator = new SqlServerToPostgreSqlMigrator();
        var plan = new MigrationPlan
        {
            SourceConnectionString = "   ",
            ExistingTargetConnectionString = "Host=pghost;Database=testdb;Username=postgres;"
        };

        var result = await migrator.ExecuteCutoverAsync(plan);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("SourceConnectionString"));
    }

    #endregion

    #region ContinuousSync and CompleteCutover — not supported

    [Fact]
    public async Task StartContinuousSyncAsync_ThrowsNotSupportedException()
    {
        var migrator = new SqlServerToPostgreSqlMigrator();
        var plan = CreateValidPlan("dbo.Users");

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => migrator.StartContinuousSyncAsync(plan));

        Assert.Contains("Continuous sync", ex.Message);
        Assert.Contains("cross-platform", ex.Message);
    }

    [Fact]
    public async Task CompleteCutoverAsync_ThrowsNotSupportedException()
    {
        var migrator = new SqlServerToPostgreSqlMigrator();

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => migrator.CompleteCutoverAsync(Guid.NewGuid()));

        Assert.Contains("CompleteCutover", ex.Message);
        Assert.Contains("ContinuousSync", ex.Message);
    }

    #endregion

    #region SourcePlatform

    [Fact]
    public void SourcePlatform_ReturnsSqlServer()
    {
        var migrator = new SqlServerToPostgreSqlMigrator();
        Assert.Equal("SqlServer", migrator.SourcePlatform);
    }

    #endregion

    #region Timeout passthrough

    [Fact]
    public async Task ExecuteCutoverAsync_PassesBulkCopyTimeoutFromPlan()
    {
        // Arrange
        var schemaDeployer = new Mock<PostgreSqlSchemaDeployer>(
            new SqlServerSchemaReader(), new DdlTranslator());
        var bulkCopier = new Mock<CrossPlatformBulkCopier>();
        var scriptExecutor = new Mock<PostgreSqlScriptExecutor>();
        var validationEngine = new Mock<PostgreSqlValidationEngine>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        int? capturedTimeout = null;
        bulkCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, IReadOnlyList<string>, IProgress<MigrationProgress>?, int?, CancellationToken>(
                (_, _, _, _, timeout, _) => capturedTimeout = timeout)
            .ReturnsAsync(0L);

        validationEngine
            .Setup(v => v.ValidateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationSummary { AllPassed = true, Results = [] });

        var migrator = new SqlServerToPostgreSqlMigrator(
            schemaDeployer.Object, bulkCopier.Object,
            scriptExecutor.Object, validationEngine.Object);

        var plan = CreateValidPlan("dbo.Users");
        plan.BulkCopyTimeoutSeconds = 1200;

        // Act
        await migrator.ExecuteCutoverAsync(plan);

        // Assert
        Assert.Equal(1200, capturedTimeout);
    }

    [Fact]
    public async Task ExecuteCutoverAsync_PassesScriptTimeoutFromPlan()
    {
        // Arrange
        var schemaDeployer = new Mock<PostgreSqlSchemaDeployer>(
            new SqlServerSchemaReader(), new DdlTranslator());
        var bulkCopier = new Mock<CrossPlatformBulkCopier>();
        var scriptExecutor = new Mock<PostgreSqlScriptExecutor>();
        var validationEngine = new Mock<PostgreSqlValidationEngine>();

        schemaDeployer
            .Setup(s => s.DeploySchemaAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        bulkCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0L);

        int? capturedTimeout = null;
        scriptExecutor
            .Setup(s => s.ExecuteScriptsAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<MigrationScript>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .Callback<string, IReadOnlyList<MigrationScript>, IProgress<MigrationProgress>?, CancellationToken, int?>(
                (_, _, _, _, timeout) => capturedTimeout = timeout)
            .Returns(Task.CompletedTask);

        validationEngine
            .Setup(v => v.ValidateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationSummary { AllPassed = true, Results = [] });

        var migrator = new SqlServerToPostgreSqlMigrator(
            schemaDeployer.Object, bulkCopier.Object,
            scriptExecutor.Object, validationEngine.Object);

        var plan = CreateValidPlan("dbo.Users");
        plan.ScriptTimeoutSeconds = 900;
        plan.PreMigrationScripts =
        [
            new MigrationScript { ScriptId = "pre1", Label = "Pre", Phase = MigrationScriptPhase.Pre, SqlContent = "SELECT 1", IsEnabled = true, Order = 0 }
        ];

        // Act
        await migrator.ExecuteCutoverAsync(plan);

        // Assert
        Assert.Equal(900, capturedTimeout);
    }

    #endregion

    #region Constructor validation

    [Fact]
    public void Constructor_NullSchemaDeployer_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SqlServerToPostgreSqlMigrator(
                null!, new CrossPlatformBulkCopier(),
                new PostgreSqlScriptExecutor(), new PostgreSqlValidationEngine()));
    }

    [Fact]
    public void Constructor_NullBulkCopier_ThrowsArgumentNullException()
    {
        var schemaReader = new SqlServerSchemaReader();
        var ddlTranslator = new DdlTranslator();
        Assert.Throws<ArgumentNullException>(() =>
            new SqlServerToPostgreSqlMigrator(
                new PostgreSqlSchemaDeployer(schemaReader, ddlTranslator), null!,
                new PostgreSqlScriptExecutor(), new PostgreSqlValidationEngine()));
    }

    [Fact]
    public void Constructor_NullScriptExecutor_ThrowsArgumentNullException()
    {
        var schemaReader = new SqlServerSchemaReader();
        var ddlTranslator = new DdlTranslator();
        Assert.Throws<ArgumentNullException>(() =>
            new SqlServerToPostgreSqlMigrator(
                new PostgreSqlSchemaDeployer(schemaReader, ddlTranslator),
                new CrossPlatformBulkCopier(), null!, new PostgreSqlValidationEngine()));
    }

    [Fact]
    public void Constructor_NullValidationEngine_ThrowsArgumentNullException()
    {
        var schemaReader = new SqlServerSchemaReader();
        var ddlTranslator = new DdlTranslator();
        Assert.Throws<ArgumentNullException>(() =>
            new SqlServerToPostgreSqlMigrator(
                new PostgreSqlSchemaDeployer(schemaReader, ddlTranslator),
                new CrossPlatformBulkCopier(), new PostgreSqlScriptExecutor(), null!));
    }

    [Fact]
    public void DefaultConstructor_CreatesInstance()
    {
        var migrator = new SqlServerToPostgreSqlMigrator();
        Assert.NotNull(migrator);
        Assert.Equal("SqlServer", migrator.SourcePlatform);
    }

    #endregion
}