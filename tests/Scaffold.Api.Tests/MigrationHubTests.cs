using Microsoft.AspNetCore.SignalR;
using Moq;
using Scaffold.Api.Hubs;

namespace Scaffold.Api.Tests;

public class MigrationHubTests
{
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly MigrationHub _hub;

    public MigrationHubTests()
    {
        _mockClients = new Mock<IHubCallerClients>();
        _mockGroups = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();

        _hub = new MigrationHub
        {
            Clients = _mockClients.Object,
            Groups = _mockGroups.Object,
            Context = _mockContext.Object
        };
    }

    #region JoinMigration

    [Fact]
    public async Task JoinMigration_AddsCallerToCorrectGroup()
    {
        // Arrange
        const string connectionId = "test-connection-id";
        const string migrationId = "migration-42";
        _mockContext.Setup(c => c.ConnectionId).Returns(connectionId);

        // Act
        await _hub.JoinMigration(migrationId);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(connectionId, migrationId, default),
            Times.Once);
    }

    [Theory]
    [InlineData("migration-1")]
    [InlineData("abc-def-123")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task JoinMigration_UsesCorrectGroupName_ForVariousMigrationIds(string migrationId)
    {
        // Arrange
        const string connectionId = "conn-123";
        _mockContext.Setup(c => c.ConnectionId).Returns(connectionId);

        // Act
        await _hub.JoinMigration(migrationId);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(connectionId, migrationId, default),
            Times.Once);
    }

    [Fact]
    public async Task JoinMigration_UsesContextConnectionId()
    {
        // Arrange
        const string expectedConnectionId = "unique-connection-xyz";
        const string migrationId = "migration-99";
        _mockContext.Setup(c => c.ConnectionId).Returns(expectedConnectionId);

        // Act
        await _hub.JoinMigration(migrationId);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(expectedConnectionId, It.IsAny<string>(), default),
            Times.Once);
    }

    #endregion

    #region LeaveMigrationGroup

    [Fact]
    public async Task LeaveMigrationGroup_RemovesCallerFromCorrectGroup()
    {
        // Arrange
        const string connectionId = "test-connection-id";
        const string migrationId = "migration-42";
        _mockContext.Setup(c => c.ConnectionId).Returns(connectionId);

        // Act
        await _hub.LeaveMigrationGroup(migrationId);

        // Assert
        _mockGroups.Verify(
            g => g.RemoveFromGroupAsync(connectionId, migrationId, default),
            Times.Once);
    }

    [Theory]
    [InlineData("migration-1")]
    [InlineData("abc-def-123")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task LeaveMigrationGroup_UsesCorrectGroupName_ForVariousMigrationIds(string migrationId)
    {
        // Arrange
        const string connectionId = "conn-456";
        _mockContext.Setup(c => c.ConnectionId).Returns(connectionId);

        // Act
        await _hub.LeaveMigrationGroup(migrationId);

        // Assert
        _mockGroups.Verify(
            g => g.RemoveFromGroupAsync(connectionId, migrationId, default),
            Times.Once);
    }

    [Fact]
    public async Task LeaveMigrationGroup_UsesContextConnectionId()
    {
        // Arrange
        const string expectedConnectionId = "unique-connection-abc";
        const string migrationId = "migration-55";
        _mockContext.Setup(c => c.ConnectionId).Returns(expectedConnectionId);

        // Act
        await _hub.LeaveMigrationGroup(migrationId);

        // Assert
        _mockGroups.Verify(
            g => g.RemoveFromGroupAsync(expectedConnectionId, It.IsAny<string>(), default),
            Times.Once);
    }

    #endregion
}
