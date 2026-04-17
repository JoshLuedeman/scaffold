using Scaffold.Migration.PostgreSql;

namespace Scaffold.Migration.Tests.PostgreSql;

public class PostgreSqlToPostgreSqlValidationEngineTests
{
    [Fact]
    public async Task ValidateAsync_EmptyTableList_ReturnsPassingSummary()
    {
        // Arrange
        var engine = new PostgreSqlToPostgreSqlValidationEngine();
        var emptyTables = new List<string>();

        // Act
        var summary = await engine.ValidateAsync(
            "Host=source;Database=db;",
            "Host=target;Database=db;",
            emptyTables);

        // Assert
        Assert.NotNull(summary);
        Assert.Empty(summary.Results);
        Assert.Equal(0, summary.TablesValidated);
        Assert.Equal(0, summary.TablesPassed);
        Assert.Equal(0, summary.TablesFailed);
        Assert.True(summary.AllPassed);
    }

    [Fact]
    public async Task ValidateAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var engine = new PostgreSqlToPostgreSqlValidationEngine();
        var tables = new List<string> { "public.users" };
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => engine.ValidateAsync(
                "Host=source;Database=db;",
                "Host=target;Database=db;",
                tables,
                cts.Token));
    }
}
