namespace Scaffold.Integration.Tests;

/// <summary>
/// Defines a shared collection fixture so that PostgreSqlMigrationFixture is created once
/// and shared across all test classes in the "PostgreSqlMigration" collection.
/// The fixture seeds the PG source, creates a PG target, and runs the migration once.
/// </summary>
[CollectionDefinition("PostgreSqlMigration")]
public class PostgreSqlMigrationCollection : ICollectionFixture<PostgreSqlMigrationFixture>
{
}
