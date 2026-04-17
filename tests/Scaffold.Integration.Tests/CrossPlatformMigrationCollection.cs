namespace Scaffold.Integration.Tests;

/// <summary>
/// Defines a shared collection fixture so that CrossPlatformMigrationFixture is created once
/// and shared across all test classes in the "CrossPlatformMigration" collection.
/// The fixture seeds SQL Server, creates a PG target, and runs the migration once.
/// </summary>
[CollectionDefinition("CrossPlatformMigration")]
public class CrossPlatformMigrationCollection : ICollectionFixture<CrossPlatformMigrationFixture>
{
}