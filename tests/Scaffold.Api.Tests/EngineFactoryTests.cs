using Microsoft.Extensions.DependencyInjection;
using Scaffold.Api.Services;
using Scaffold.Assessment.SqlServer;
using Scaffold.Core.Enums;
using Scaffold.Core.Interfaces;
using Scaffold.Migration.SqlServer;

namespace Scaffold.Api.Tests;

public class EngineFactoryTests
{
    #region AssessmentEngineFactory

    [Fact]
    public void AssessmentFactory_Create_SqlServer_Returns_SqlServerAssessor()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<SqlServerConnectionFactory>();
        services.AddLogging();
        services.AddScoped<SqlServerAssessor>();
        var provider = services.BuildServiceProvider();
        var factory = new AssessmentEngineFactory(provider);

        // Act
        var engine = factory.Create(DatabasePlatform.SqlServer);

        // Assert
        Assert.NotNull(engine);
        Assert.IsType<SqlServerAssessor>(engine);
        Assert.Equal("SqlServer", engine.SourcePlatform);
    }

    [Fact]
    public void AssessmentFactory_Create_Unsupported_Throws_NotSupportedException()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var factory = new AssessmentEngineFactory(provider);

        // Act & Assert
        var ex = Assert.Throws<NotSupportedException>(() => factory.Create(DatabasePlatform.PostgreSql));
        Assert.Contains("PostgreSql", ex.Message);
    }

    [Fact]
    public void AssessmentFactory_SupportedPlatforms_Includes_SqlServer()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var factory = new AssessmentEngineFactory(provider);

        // Act
        var platforms = factory.SupportedPlatforms;

        // Assert
        Assert.Contains(DatabasePlatform.SqlServer, platforms);
    }

    [Fact]
    public void AssessmentFactory_SupportedPlatforms_Does_Not_Include_PostgreSql()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var factory = new AssessmentEngineFactory(provider);

        // Act
        var platforms = factory.SupportedPlatforms;

        // Assert
        Assert.DoesNotContain(DatabasePlatform.PostgreSql, platforms);
    }

    #endregion

    #region MigrationEngineFactory

    [Fact]
    public void MigrationFactory_Create_SqlServer_Returns_SqlServerMigrator()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<SqlServerMigrator>();
        var provider = services.BuildServiceProvider();
        var factory = new MigrationEngineFactory(provider);

        // Act
        var engine = factory.Create(DatabasePlatform.SqlServer);

        // Assert
        Assert.NotNull(engine);
        Assert.IsType<SqlServerMigrator>(engine);
        Assert.Equal("SqlServer", engine.SourcePlatform);
    }

    [Fact]
    public void MigrationFactory_Create_Unsupported_Throws_NotSupportedException()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var factory = new MigrationEngineFactory(provider);

        // Act & Assert
        var ex = Assert.Throws<NotSupportedException>(() => factory.Create(DatabasePlatform.PostgreSql));
        Assert.Contains("PostgreSql", ex.Message);
    }

    [Fact]
    public void MigrationFactory_SupportedPlatforms_Includes_SqlServer()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var factory = new MigrationEngineFactory(provider);

        // Act
        var platforms = factory.SupportedPlatforms;

        // Assert
        Assert.Contains(DatabasePlatform.SqlServer, platforms);
    }

    [Fact]
    public void MigrationFactory_SupportedPlatforms_Does_Not_Include_PostgreSql()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var factory = new MigrationEngineFactory(provider);

        // Act
        var platforms = factory.SupportedPlatforms;

        // Assert
        Assert.DoesNotContain(DatabasePlatform.PostgreSql, platforms);
    }

    #endregion

    #region Error message content

    [Theory]
    [InlineData(DatabasePlatform.PostgreSql)]
    public void AssessmentFactory_Create_Unsupported_Message_Contains_Platform(DatabasePlatform platform)
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var factory = new AssessmentEngineFactory(provider);

        // Act & Assert
        var ex = Assert.Throws<NotSupportedException>(() => factory.Create(platform));
        Assert.Contains(platform.ToString(), ex.Message);
        Assert.Contains("assessment engine", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(DatabasePlatform.PostgreSql)]
    public void MigrationFactory_Create_Unsupported_Message_Contains_Platform(DatabasePlatform platform)
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var factory = new MigrationEngineFactory(provider);

        // Act & Assert
        var ex = Assert.Throws<NotSupportedException>(() => factory.Create(platform));
        Assert.Contains(platform.ToString(), ex.Message);
        Assert.Contains("migration engine", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
