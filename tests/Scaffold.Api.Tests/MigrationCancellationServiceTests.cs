using Scaffold.Api.Services;

namespace Scaffold.Api.Tests;

public class MigrationCancellationServiceTests
{
    [Fact]
    public void Register_ReturnsCancellationToken_NotCancelled()
    {
        var service = new MigrationCancellationService();
        var migrationId = Guid.NewGuid();

        var token = service.Register(migrationId);

        Assert.False(token.IsCancellationRequested);
    }

    [Fact]
    public void Cancel_RegisteredMigration_ReturnsTrueAndCancelsToken()
    {
        var service = new MigrationCancellationService();
        var migrationId = Guid.NewGuid();
        var token = service.Register(migrationId);

        var result = service.Cancel(migrationId);

        Assert.True(result);
        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public void Cancel_UnregisteredMigration_ReturnsFalse()
    {
        var service = new MigrationCancellationService();
        var migrationId = Guid.NewGuid();

        var result = service.Cancel(migrationId);

        Assert.False(result);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_ReturnsFalseOnSecondCall()
    {
        var service = new MigrationCancellationService();
        var migrationId = Guid.NewGuid();
        service.Register(migrationId);

        var first = service.Cancel(migrationId);
        var second = service.Cancel(migrationId);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void Unregister_RemovesMigration()
    {
        var service = new MigrationCancellationService();
        var migrationId = Guid.NewGuid();
        service.Register(migrationId);

        service.Unregister(migrationId);

        Assert.False(service.IsActive(migrationId));
    }

    [Fact]
    public void Unregister_NonExistentMigration_DoesNotThrow()
    {
        var service = new MigrationCancellationService();

        // Should not throw
        service.Unregister(Guid.NewGuid());
    }

    [Fact]
    public void IsActive_ReturnsTrueForRegistered()
    {
        var service = new MigrationCancellationService();
        var migrationId = Guid.NewGuid();
        service.Register(migrationId);

        Assert.True(service.IsActive(migrationId));
    }

    [Fact]
    public void IsActive_ReturnsFalseForUnregistered()
    {
        var service = new MigrationCancellationService();

        Assert.False(service.IsActive(Guid.NewGuid()));
    }

    [Fact]
    public void Cancel_DisposesTokenSource_SubsequentUnregisterSafe()
    {
        var service = new MigrationCancellationService();
        var migrationId = Guid.NewGuid();
        service.Register(migrationId);

        // Cancel should dispose the token source
        service.Cancel(migrationId);

        // IsActive should be false after cancel (token removed)
        Assert.False(service.IsActive(migrationId));

        // Unregister should be safe even after cancel disposed the source
        service.Unregister(migrationId);
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentRegisterAndCancel()
    {
        var service = new MigrationCancellationService();
        var ids = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList();

        // Register all concurrently
        await Task.WhenAll(ids.Select(id => Task.Run(() => service.Register(id))));

        // All should be active
        Assert.All(ids, id => Assert.True(service.IsActive(id)));

        // Cancel all concurrently
        var results = new bool[ids.Count];
        await Task.WhenAll(ids.Select((id, i) => Task.Run(() => results[i] = service.Cancel(id))));

        // All cancellations should succeed
        Assert.All(results, r => Assert.True(r));

        // None should be active after cancellation
        Assert.All(ids, id => Assert.False(service.IsActive(id)));
    }

    [Fact]
    public void Register_OverwritesPreviousRegistration()
    {
        var service = new MigrationCancellationService();
        var migrationId = Guid.NewGuid();

        var token1 = service.Register(migrationId);
        var token2 = service.Register(migrationId);

        // The second registration should produce a different token
        // First token may or may not be cancelled depending on implementation,
        // but the service should be active with the new token
        Assert.True(service.IsActive(migrationId));
        Assert.False(token2.IsCancellationRequested);
    }
}
