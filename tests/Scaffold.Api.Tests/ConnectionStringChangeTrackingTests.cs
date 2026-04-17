using Microsoft.EntityFrameworkCore;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;
using Scaffold.Infrastructure.Data;

namespace Scaffold.Api.Tests;

/// <summary>
/// Regression tests verifying that decrypted connection strings are NOT persisted
/// to the database when EF change tracking IsModified is set to false.
/// Guards against CVE where plaintext credentials overwrite encrypted values.
/// </summary>
public class ConnectionStringChangeTrackingTests
{
    [Fact]
    public async Task SaveChangesAsync_DoesNotPersistDecryptedConnectionStrings_WhenIsModifiedIsFalse()
    {
        // Arrange — create an in-memory database with a plan containing "encrypted" values
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ScaffoldDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var planId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        const string encryptedSource = "CfDJ8AAAA_encrypted_source_connection_string";
        const string encryptedTarget = "CfDJ8AAAA_encrypted_target_connection_string";
        const string decryptedSource = "Server=myserver;Database=SourceDB;User Id=sa;Password=P@ssw0rd;";
        const string decryptedTarget = "Server=myserver;Database=TargetDB;User Id=sa;Password=P@ssw0rd;";

        // Seed the plan with encrypted connection strings
        using (var seedCtx = new ScaffoldDbContext(options))
        {
            var project = new MigrationProject
            {
                Id = projectId,
                Name = "ChangeTrackingTest",
                CreatedBy = "test",
                Status = ProjectStatus.MigrationPlanned,
                SourceConnection = new ConnectionInfo
                {
                    Id = Guid.NewGuid(),
                    Server = "localhost",
                    Database = "TestDB",
                    UseSqlAuthentication = true
                },
                MigrationPlan = new MigrationPlan
                {
                    Id = planId,
                    Strategy = MigrationStrategy.Cutover,
                    IsApproved = true,
                    Status = MigrationStatus.Running,
                    SourceConnectionString = encryptedSource,
                    ExistingTargetConnectionString = encryptedTarget
                }
            };

            seedCtx.MigrationProjects.Add(project);
            await seedCtx.SaveChangesAsync();
        }

        // Act — simulate what the controller/scheduler does: load, decrypt, mark unmodified, save
        using (var ctx = new ScaffoldDbContext(options))
        {
            var plan = await ctx.MigrationPlans.FirstAsync(p => p.Id == planId);

            // Simulate decryption: overwrite with plaintext values
            plan.SourceConnectionString = decryptedSource;
            plan.ExistingTargetConnectionString = decryptedTarget;

            // Apply the security fix: mark connection string properties as not modified
            ctx.Entry(plan).Property(p => p.SourceConnectionString).IsModified = false;
            ctx.Entry(plan).Property(p => p.ExistingTargetConnectionString).IsModified = false;

            // Change another property that SHOULD be persisted (to prove SaveChanges runs)
            plan.Status = MigrationStatus.Completed;

            await ctx.SaveChangesAsync();
        }

        // Assert — reload from the database and verify encrypted values are preserved
        using (var verifyCtx = new ScaffoldDbContext(options))
        {
            var reloaded = await verifyCtx.MigrationPlans.FirstAsync(p => p.Id == planId);

            Assert.Equal(encryptedSource, reloaded.SourceConnectionString);
            Assert.Equal(encryptedTarget, reloaded.ExistingTargetConnectionString);
            // Confirm the status change DID persist (SaveChanges actually ran)
            Assert.Equal(MigrationStatus.Completed, reloaded.Status);
        }
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsDecryptedConnectionStrings_WhenIsModifiedNotReset()
    {
        // This test documents the vulnerable behavior (without the fix)
        // to confirm our fix actually prevents it.
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ScaffoldDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var planId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        const string encryptedSource = "CfDJ8AAAA_encrypted_source_connection_string";
        const string encryptedTarget = "CfDJ8AAAA_encrypted_target_connection_string";
        const string decryptedSource = "Server=myserver;Database=SourceDB;User Id=sa;Password=P@ssw0rd;";
        const string decryptedTarget = "Server=myserver;Database=TargetDB;User Id=sa;Password=P@ssw0rd;";

        // Seed the plan with encrypted connection strings
        using (var seedCtx = new ScaffoldDbContext(options))
        {
            var project = new MigrationProject
            {
                Id = projectId,
                Name = "VulnerabilityTest",
                CreatedBy = "test",
                Status = ProjectStatus.MigrationPlanned,
                SourceConnection = new ConnectionInfo
                {
                    Id = Guid.NewGuid(),
                    Server = "localhost",
                    Database = "TestDB",
                    UseSqlAuthentication = true
                },
                MigrationPlan = new MigrationPlan
                {
                    Id = planId,
                    Strategy = MigrationStrategy.Cutover,
                    IsApproved = true,
                    Status = MigrationStatus.Running,
                    SourceConnectionString = encryptedSource,
                    ExistingTargetConnectionString = encryptedTarget
                }
            };

            seedCtx.MigrationProjects.Add(project);
            await seedCtx.SaveChangesAsync();
        }

        // Act — simulate the vulnerable path: overwrite with plaintext WITHOUT marking unmodified
        using (var ctx = new ScaffoldDbContext(options))
        {
            var plan = await ctx.MigrationPlans.FirstAsync(p => p.Id == planId);

            plan.SourceConnectionString = decryptedSource;
            plan.ExistingTargetConnectionString = decryptedTarget;

            // Deliberately NOT setting IsModified = false (the vulnerable behavior)
            await ctx.SaveChangesAsync();
        }

        // Assert — without the fix, plaintext values WOULD be persisted
        using (var verifyCtx = new ScaffoldDbContext(options))
        {
            var reloaded = await verifyCtx.MigrationPlans.FirstAsync(p => p.Id == planId);

            // This proves that without IsModified=false, EF persists the decrypted values
            Assert.Equal(decryptedSource, reloaded.SourceConnectionString);
            Assert.Equal(decryptedTarget, reloaded.ExistingTargetConnectionString);
        }
    }
}
