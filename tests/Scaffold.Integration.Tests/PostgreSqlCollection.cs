namespace Scaffold.Integration.Tests;

/// <summary>
/// Defines a shared collection fixture so that PostgreSqlFixture is created once
/// and shared across all test classes in the "PostgreSql" collection.
/// </summary>
[CollectionDefinition("PostgreSql")]
public class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
}