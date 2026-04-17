using Moq;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Core.Models;
using Scaffold.Migration.PostgreSql;
using Scaffold.Migration.PostgreSql.Models;
using Scaffold.Migration.SqlServer;

namespace Scaffold.Migration.Tests.PostgreSql;

public class PostgreSqlMigratorTests
{
    private static MigrationPlan CreateValidPlan(params string[] tables) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        SourceConnectionString = "Host=source;Database=testdb;Username=postgres;",
        ExistingTargetConnectionString = "Host=target;Database=testdb;Username=postgres;",
        SourcePlatform = DatabasePlatform.PostgreSql,
        TargetPlatform = DatabasePlatform.PostgreSql,
        IncludedObjects = tables.ToList()
    };

    private static PostgreSqlMigrator CreateMigrator(
        Mock<PostgreSqlSchemaExtractor>? schemaExtractor = null,
        Mock<PostgreSqlDdlGenerator>? ddlGenerator = null,
        Mock<PostgreSqlBulkCopier>? bulkCopier = null,
        Mock<PostgreSqlScriptExecutor>? scriptExecutor = null,
        Mock<PostgreSqlToPostgreSqlValidationEngine>? validationEngine = null,
        Mock<AzureExtensionHandler>? extensionHandler = null)
    {
        return new PostgreSqlMigrator(
            (schemaExtractor ?? new Mock<PostgreSqlSchemaExtractor>()).Object,
            (ddlGenerator ?? new Mock<PostgreSqlDdlGenerator>()).Object,
            (bulkCopier ?? new Mock<PostgreSqlBulkCopier>()).Object,
            (scriptExecutor ?? new Mock<PostgreSqlScriptExecutor>()).Object,
            (validationEngine ?? new Mock<PostgreSqlToPostgreSqlValidationEngine>()).Object,
            (extensionHandler ?? new Mock<AzureExtensionHandler>()).Object);
    }

    #region Constructor null guards

    [Fact]
    public void Constructor_NullSchemaExtractor_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new PostgreSqlMigrator(
            null!,
            new PostgreSqlDdlGenerator(),
            new PostgreSqlBulkCopier(),
            new PostgreSqlScriptExecutor(),
            new PostgreSqlToPostgreSqlValidationEngine(),
            new AzureExtensionHandler()));
        Assert.Equal("schemaExtractor", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullDdlGenerator_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new PostgreSqlMigrator(
            new PostgreSqlSchemaExtractor(),
            null!,
            new PostgreSqlBulkCopier(),
            new PostgreSqlScriptExecutor(),
            new PostgreSqlToPostgreSqlValidationEngine(),
            new AzureExtensionHandler()));
        Assert.Equal("ddlGenerator", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullBulkCopier_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new PostgreSqlMigrator(
            new PostgreSqlSchemaExtractor(),
            new PostgreSqlDdlGenerator(),
            null!,
            new PostgreSqlScriptExecutor(),
            new PostgreSqlToPostgreSqlValidationEngine(),
            new AzureExtensionHandler()));
        Assert.Equal("bulkCopier", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullScriptExecutor_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new PostgreSqlMigrator(
            new PostgreSqlSchemaExtractor(),
            new PostgreSqlDdlGenerator(),
            new PostgreSqlBulkCopier(),
            null!,
            new PostgreSqlToPostgreSqlValidationEngine(),
            new AzureExtensionHandler()));
        Assert.Equal("scriptExecutor", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullValidationEngine_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new PostgreSqlMigrator(
            new PostgreSqlSchemaExtractor(),
            new PostgreSqlDdlGenerator(),
            new PostgreSqlBulkCopier(),
            new PostgreSqlScriptExecutor(),
            null!,
            new AzureExtensionHandler()));
        Assert.Equal("validationEngine", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullExtensionHandler_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new PostgreSqlMigrator(
            new PostgreSqlSchemaExtractor(),
            new PostgreSqlDdlGenerator(),
            new PostgreSqlBulkCopier(),
            new PostgreSqlScriptExecutor(),
            new PostgreSqlToPostgreSqlValidationEngine(),
            null!));
        Assert.Equal("extensionHandler", ex.ParamName);
    }

    #endregion

    #region SourcePlatform

    [Fact]
    public void SourcePlatform_ReturnsPostgreSql()
    {
        var migrator = CreateMigrator();
        Assert.Equal("PostgreSql", migrator.SourcePlatform);
    }

    #endregion

    #region ExecuteCutoverAsync — connection string validation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteCutoverAsync_NullOrEmptySourceConnectionString_ReturnsFailure(string? source)
    {
        var migrator = CreateMigrator();
        var plan = CreateValidPlan();
        plan.SourceConnectionString = source;

        var result = await migrator.ExecuteCutoverAsync(plan);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("SourceConnectionString"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteCutoverAsync_NullOrEmptyTargetConnectionString_ReturnsFailure(string? target)
    {
        var migrator = CreateMigrator();
        var plan = CreateValidPlan();
        plan.ExistingTargetConnectionString = target;

        var result = await migrator.ExecuteCutoverAsync(plan);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("ExistingTargetConnectionString"));
    }

    #endregion

    #region StartContinuousSyncAsync — validates connection strings

    [Fact]
    public async Task StartContinuousSyncAsync_MissingSourceConnectionString_ThrowsArgumentException()
    {
        var migrator = CreateMigrator();
        var plan = CreateValidPlan();
        plan.SourceConnectionString = null;

        await Assert.ThrowsAsync<ArgumentException>(
            () => migrator.StartContinuousSyncAsync(plan));
    }

    [Fact]
    public async Task StartContinuousSyncAsync_MissingTargetConnectionString_ThrowsArgumentException()
    {
        var migrator = CreateMigrator();
        var plan = CreateValidPlan();
        plan.ExistingTargetConnectionString = null;

        await Assert.ThrowsAsync<ArgumentException>(
            () => migrator.StartContinuousSyncAsync(plan));
    }

    #endregion

    #region CompleteCutoverAsync — requires active sync

    [Fact]
    public async Task CompleteCutoverAsync_WithoutStartingSync_ThrowsInvalidOperationException()
    {
        var migrator = CreateMigrator();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => migrator.CompleteCutoverAsync(Guid.NewGuid()));
        Assert.Contains("StartContinuousSyncAsync", ex.Message);
    }

    #endregion

    #region ExecuteCutoverAsync — extension failure causes early return

    [Fact]
    public async Task ExecuteCutoverAsync_ExtensionFailure_ReturnsFailedResult()
    {
        // Arrange
        var schemaExtractor = new Mock<PostgreSqlSchemaExtractor>();
        var extensionHandler = new Mock<AzureExtensionHandler>();

        schemaExtractor
            .Setup(s => s.ExtractSchemaAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PgSchemaSnapshot
            {
                Extensions = ["pg_trgm", "unsupported_ext"],
                Tables = []
            });

        extensionHandler
            .Setup(e => e.InstallExtensionsAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtensionMigrationResult
            {
                Installed = [],
                Failed = ["unsupported_ext"],
                Warnings =
                [
                    new ExtensionWarning
                    {
                        ExtensionName = "unsupported_ext",
                        Message = "Failed to install extension 'unsupported_ext'",
                        Severity = ExtensionWarningSeverity.Error
                    }
                ]
            });

        var migrator = CreateMigrator(
            schemaExtractor: schemaExtractor,
            extensionHandler: extensionHandler);

        var plan = CreateValidPlan();

        // Act
        var result = await migrator.ExecuteCutoverAsync(plan);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Contains("unsupported_ext", result.Errors[0]);
    }

    #endregion

    #region ExecuteCutoverAsync — progress reporting

    [Fact]
    public async Task ExecuteCutoverAsync_ReportsProgressForEachPhase()
    {
        // Arrange
        var schemaExtractor = new Mock<PostgreSqlSchemaExtractor>();
        var ddlGenerator = new Mock<PostgreSqlDdlGenerator>();
        var bulkCopier = new Mock<PostgreSqlBulkCopier>();
        var scriptExecutor = new Mock<PostgreSqlScriptExecutor>();
        var validationEngine = new Mock<PostgreSqlToPostgreSqlValidationEngine>();
        var extensionHandler = new Mock<AzureExtensionHandler>();

        var snapshot = new PgSchemaSnapshot
        {
            Extensions = [],
            Tables =
            [
                new PgTableDefinition { Schema = "public", TableName = "users" }
            ]
        };

        schemaExtractor
            .Setup(s => s.ExtractSchemaAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        extensionHandler
            .Setup(e => e.InstallExtensionsAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtensionMigrationResult());

        ddlGenerator
            .Setup(d => d.GenerateDdl(It.IsAny<PgSchemaSnapshot>()))
            .Returns([]);

        bulkCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(100L);

        bulkCopier
            .Setup(b => b.ResetSequencesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        validationEngine
            .Setup(v => v.ValidateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationSummary { AllPassed = true, Results = [] });

        var reportedPhases = new List<string>();
        var progress = new Mock<IProgress<MigrationProgress>>();
        progress.Setup(p => p.Report(It.IsAny<MigrationProgress>()))
            .Callback<MigrationProgress>(mp => reportedPhases.Add(mp.Phase));

        var migrator = CreateMigrator(
            schemaExtractor: schemaExtractor,
            ddlGenerator: ddlGenerator,
            bulkCopier: bulkCopier,
            scriptExecutor: scriptExecutor,
            validationEngine: validationEngine,
            extensionHandler: extensionHandler);

        var plan = CreateValidPlan();

        // Act
        var result = await migrator.ExecuteCutoverAsync(plan, progress.Object);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("SchemaExtraction", reportedPhases);
        Assert.Contains("Extensions", reportedPhases);
        Assert.Contains("SchemaDeployment", reportedPhases);
        Assert.Contains("DataMigration", reportedPhases);
        Assert.Contains("SequenceReset", reportedPhases);
        Assert.Contains("Validation", reportedPhases);
    }

    #endregion

    #region ExecuteCutoverAsync — validation results propagated

    [Fact]
    public async Task ExecuteCutoverAsync_PropagatesValidationResults()
    {
        // Arrange
        var schemaExtractor = new Mock<PostgreSqlSchemaExtractor>();
        var ddlGenerator = new Mock<PostgreSqlDdlGenerator>();
        var bulkCopier = new Mock<PostgreSqlBulkCopier>();
        var validationEngine = new Mock<PostgreSqlToPostgreSqlValidationEngine>();
        var extensionHandler = new Mock<AzureExtensionHandler>();

        var snapshot = new PgSchemaSnapshot
        {
            Extensions = [],
            Tables =
            [
                new PgTableDefinition { Schema = "public", TableName = "orders" }
            ]
        };

        schemaExtractor
            .Setup(s => s.ExtractSchemaAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        extensionHandler
            .Setup(e => e.InstallExtensionsAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtensionMigrationResult());

        ddlGenerator
            .Setup(d => d.GenerateDdl(It.IsAny<PgSchemaSnapshot>()))
            .Returns([]);

        bulkCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(500L);

        bulkCopier
            .Setup(b => b.ResetSequencesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var validationResults = new List<ValidationResult>
        {
            new() { TableName = "public.orders", SourceRowCount = 500, TargetRowCount = 500, ChecksumMatch = true }
        };

        validationEngine
            .Setup(v => v.ValidateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationSummary
            {
                AllPassed = true,
                Results = validationResults,
                TablesValidated = 1,
                TablesPassed = 1,
                TablesFailed = 0
            });

        var migrator = CreateMigrator(
            schemaExtractor: schemaExtractor,
            ddlGenerator: ddlGenerator,
            bulkCopier: bulkCopier,
            validationEngine: validationEngine,
            extensionHandler: extensionHandler);

        var plan = CreateValidPlan();

        // Act
        var result = await migrator.ExecuteCutoverAsync(plan);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(500L, result.RowsMigrated);
        Assert.Single(result.Validations);
        Assert.Equal("public.orders", result.Validations[0].TableName);
        Assert.True(result.Validations[0].Passed);
    }

    #endregion

    #region ExecuteCutoverAsync — validation failure → result not successful

    [Fact]
    public async Task ExecuteCutoverAsync_ValidationFailure_ReturnsFailedResult()
    {
        // Arrange
        var schemaExtractor = new Mock<PostgreSqlSchemaExtractor>();
        var ddlGenerator = new Mock<PostgreSqlDdlGenerator>();
        var bulkCopier = new Mock<PostgreSqlBulkCopier>();
        var validationEngine = new Mock<PostgreSqlToPostgreSqlValidationEngine>();
        var extensionHandler = new Mock<AzureExtensionHandler>();

        var snapshot = new PgSchemaSnapshot
        {
            Extensions = [],
            Tables = [new PgTableDefinition { Schema = "public", TableName = "orders" }]
        };

        schemaExtractor
            .Setup(s => s.ExtractSchemaAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        extensionHandler
            .Setup(e => e.InstallExtensionsAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtensionMigrationResult());

        ddlGenerator
            .Setup(d => d.GenerateDdl(It.IsAny<PgSchemaSnapshot>()))
            .Returns([]);

        bulkCopier
            .Setup(b => b.CopyDataAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(500L);

        bulkCopier
            .Setup(b => b.ResetSequencesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        validationEngine
            .Setup(v => v.ValidateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationSummary
            {
                AllPassed = false,
                Results = [new ValidationResult { TableName = "public.orders", SourceRowCount = 500, TargetRowCount = 400, ChecksumMatch = false }],
                TablesValidated = 1,
                TablesPassed = 0,
                TablesFailed = 1
            });

        var migrator = CreateMigrator(
            schemaExtractor: schemaExtractor,
            ddlGenerator: ddlGenerator,
            bulkCopier: bulkCopier,
            validationEngine: validationEngine,
            extensionHandler: extensionHandler);

        var plan = CreateValidPlan();

        // Act
        var result = await migrator.ExecuteCutoverAsync(plan);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Validations);
        Assert.False(result.Validations[0].Passed);
    }

    #endregion

    #region ExecuteCutoverAsync — exception handling

    [Fact]
    public async Task ExecuteCutoverAsync_ExceptionDuringExecution_CapturedInResult()
    {
        // Arrange
        var schemaExtractor = new Mock<PostgreSqlSchemaExtractor>();
        schemaExtractor
            .Setup(s => s.ExtractSchemaAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection refused"));

        var migrator = CreateMigrator(schemaExtractor: schemaExtractor);
        var plan = CreateValidPlan();

        // Act
        var result = await migrator.ExecuteCutoverAsync(plan);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Contains("Connection refused", result.Errors[0]);
        Assert.NotNull(result.CompletedAt);
    }

    [Fact]
    public async Task ExecuteCutoverAsync_OperationCancelled_IsNotCaught()
    {
        // Arrange
        var schemaExtractor = new Mock<PostgreSqlSchemaExtractor>();
        schemaExtractor
            .Setup(s => s.ExtractSchemaAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>?>(),
                It.IsAny<IProgress<MigrationProgress>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var migrator = CreateMigrator(schemaExtractor: schemaExtractor);
        var plan = CreateValidPlan();

        // Act & Assert — OperationCanceledException propagates
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => migrator.ExecuteCutoverAsync(plan));
    }

    #endregion

    #region IMigrationEngine interface

    [Fact]
    public void ImplementsIMigrationEngine()
    {
        var migrator = CreateMigrator();
        Assert.IsAssignableFrom<IMigrationEngine>(migrator);
    }

    #endregion
}
