using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Scaffold.Core.Enums;
using Scaffold.Core.Models;

namespace Scaffold.Infrastructure.Data;

public class ScaffoldDbContext : DbContext
{
    public ScaffoldDbContext(DbContextOptions<ScaffoldDbContext> options) : base(options) { }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = utcNow;
                    entry.Entity.UpdatedAt = utcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = utcNow;
                    // Prevent CreatedAt from being changed on update
                    entry.Property(nameof(AuditableEntity.CreatedAt)).IsModified = false;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    public DbSet<MigrationProject> MigrationProjects => Set<MigrationProject>();
    public DbSet<ConnectionInfo> ConnectionInfos => Set<ConnectionInfo>();
    public DbSet<AssessmentReport> AssessmentReports => Set<AssessmentReport>();
    public DbSet<MigrationPlan> MigrationPlans => Set<MigrationPlan>();
    public DbSet<MigrationResult> MigrationResults => Set<MigrationResult>();
    public DbSet<MigrationProgressRecord> MigrationProgressRecords => Set<MigrationProgressRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureMigrationProject(modelBuilder);
        ConfigureConnectionInfo(modelBuilder);
        ConfigureAssessmentReport(modelBuilder);
        ConfigureMigrationPlan(modelBuilder);
        ConfigureMigrationResult(modelBuilder);
        ConfigureMigrationProgressRecord(modelBuilder);
    }

    private static void ConfigureMigrationProject(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationProject>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(2000);
            entity.Property(p => p.CreatedBy).HasMaxLength(200).IsRequired();

            entity.Property(p => p.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.HasOne(p => p.SourceConnection)
                .WithOne()
                .HasForeignKey<ConnectionInfo>("MigrationProjectId")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.Assessment)
                .WithOne()
                .HasForeignKey<AssessmentReport>(a => a.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.MigrationPlan)
                .WithOne()
                .HasForeignKey<MigrationPlan>(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureConnectionInfo(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConnectionInfo>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Server).HasMaxLength(500).IsRequired();
            entity.Property(c => c.Database).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Username).HasMaxLength(200);
            entity.Property(c => c.KeyVaultSecretUri).HasMaxLength(500);
        });
    }

    private static void ConfigureAssessmentReport(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssessmentReport>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Risk)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.OwnsOne(a => a.Schema, schema =>
            {
                schema.ToJson();
                schema.OwnsMany(s => s.Objects);
            });

            entity.OwnsOne(a => a.DataProfile, dp =>
            {
                dp.ToJson();
                dp.OwnsMany(d => d.Tables);
            });

            entity.OwnsOne(a => a.Performance, p => p.ToJson());

            entity.OwnsMany(a => a.CompatibilityIssues, ci => ci.ToJson());

            entity.OwnsOne(a => a.Recommendation, r =>
            {
                r.ToJson();
                r.Property(x => x.EstimatedMonthlyCostUsd).HasPrecision(18, 2);
            });
        });
    }

    private static void ConfigureMigrationPlan(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationPlan>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Strategy)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(p => p.IncludedObjects)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(StringListComparer());

            entity.Property(p => p.ExcludedObjects)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(StringListComparer());

            entity.OwnsMany(p => p.PreMigrationScripts, s =>
            {
                s.ToJson("PreMigrationScriptsJson");
            });
            entity.OwnsMany(p => p.PostMigrationScripts, s =>
            {
                s.ToJson("PostMigrationScriptsJson");
            });

            entity.Property(p => p.ExistingTargetConnectionString).HasMaxLength(1000);
            entity.Property(p => p.ApprovedBy).HasMaxLength(200);
            entity.Property(p => p.RejectedBy).HasMaxLength(200);
            entity.Property(p => p.RejectionReason).HasMaxLength(2000);

            entity.Property(p => p.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.OwnsOne(p => p.TargetTier, t =>
            {
                t.Property(r => r.EstimatedMonthlyCostUsd).HasPrecision(18, 2);
                t.ToJson();
            });
        });
    }

    private static void ConfigureMigrationResult(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationResult>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.HasOne<MigrationProject>()
                .WithMany()
                .HasForeignKey(r => r.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.OwnsMany(r => r.Validations, v =>
            {
                v.ToJson();
                v.Ignore(x => x.Passed);
            });

            entity.Property(r => r.Errors)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(StringListComparer());
        });
    }

    private static void ConfigureMigrationProgressRecord(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationProgressRecord>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Phase).HasMaxLength(100);
            entity.Property(r => r.CurrentTable).HasMaxLength(500);
            entity.Property(r => r.Message).HasMaxLength(2000);
            entity.HasIndex(r => r.MigrationId);
        });
    }

    private static ValueComparer<List<string>> StringListComparer() =>
        new(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<List<string>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);
}
