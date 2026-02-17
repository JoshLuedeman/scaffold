namespace Scaffold.Integration.Tests;

/// <summary>
/// Defines a shared collection fixture so that SqlServerFixture is created once
/// and shared across all test classes in the "SqlServer" collection.
/// </summary>
[CollectionDefinition("SqlServer")]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
}
