using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Assessment.Tests;

public class MigrationPlanPlatformTests
{
    [Fact]
    public void MigrationPlan_DefaultSourcePlatform_IsSqlServer()
    {
        var plan = new MigrationPlan();

        Assert.Equal(DatabasePlatform.SqlServer, plan.SourcePlatform);
    }

    [Fact]
    public void MigrationPlan_DefaultTargetPlatform_IsSqlServer()
    {
        var plan = new MigrationPlan();

        Assert.Equal(DatabasePlatform.SqlServer, plan.TargetPlatform);
    }

    [Fact]
    public void MigrationPlan_SetBothPlatforms_ToPostgreSql()
    {
        var plan = new MigrationPlan
        {
            SourcePlatform = DatabasePlatform.PostgreSql,
            TargetPlatform = DatabasePlatform.PostgreSql
        };

        Assert.Equal(DatabasePlatform.PostgreSql, plan.SourcePlatform);
        Assert.Equal(DatabasePlatform.PostgreSql, plan.TargetPlatform);
    }

    [Theory]
    [InlineData(DatabasePlatform.SqlServer, DatabasePlatform.PostgreSql)]
    [InlineData(DatabasePlatform.PostgreSql, DatabasePlatform.SqlServer)]
    [InlineData(DatabasePlatform.PostgreSql, DatabasePlatform.PostgreSql)]
    public void MigrationPlan_SupportsCrossPlatformCombinations(
        DatabasePlatform source, DatabasePlatform target)
    {
        var plan = new MigrationPlan
        {
            SourcePlatform = source,
            TargetPlatform = target
        };

        Assert.Equal(source, plan.SourcePlatform);
        Assert.Equal(target, plan.TargetPlatform);
    }
}
