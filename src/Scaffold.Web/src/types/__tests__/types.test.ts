import type {
  ProjectStatus,
  MigrationStrategy,
  RiskRating,
  ConnectionInfo,
  SchemaObject,
  SchemaInventory,
  AssessmentReport,
  MigrationPlan,
  MigrationResult,
  MigrationProject,
  ValidationResult,
} from '../index';

describe('TypeScript types', () => {
  it('ProjectStatus accepts all valid values', () => {
    const statuses: ProjectStatus[] = [
      'Created',
      'Assessing',
      'Assessed',
      'PlanningMigration',
      'MigrationPlanned',
      'Migrating',
      'MigrationComplete',
      'Failed',
    ];
    expect(statuses).toHaveLength(8);
  });

  it('MigrationStrategy accepts valid values', () => {
    const strategies: MigrationStrategy[] = ['Cutover', 'ContinuousSync'];
    expect(strategies).toHaveLength(2);
  });

  it('RiskRating accepts valid values', () => {
    const ratings: RiskRating[] = ['Low', 'Medium', 'High'];
    expect(ratings).toHaveLength(3);
  });

  it('ConnectionInfo compiles with required fields', () => {
    const conn: ConnectionInfo = {
      id: '1',
      server: 'localhost',
      database: 'testdb',
      port: 1433,
      useSqlAuthentication: false,
      trustServerCertificate: true,
    };
    expect(conn.server).toBe('localhost');
  });

  it('SchemaObject compiles correctly', () => {
    const obj: SchemaObject = { name: 'Users', schema: 'dbo', objectType: 'Table' };
    expect(obj.name).toBe('Users');
  });

  it('SchemaInventory compiles correctly', () => {
    const inv: SchemaInventory = {
      tableCount: 10,
      viewCount: 2,
      storedProcedureCount: 5,
      indexCount: 15,
      triggerCount: 1,
      objects: [],
    };
    expect(inv.tableCount).toBe(10);
  });

  it('AssessmentReport compiles with all fields', () => {
    const report: AssessmentReport = {
      id: 'r1',
      projectId: 'p1',
      generatedAt: '2025-01-01T00:00:00Z',
      schema: { tableCount: 0, viewCount: 0, storedProcedureCount: 0, indexCount: 0, triggerCount: 0, objects: [] },
      dataProfile: { totalRowCount: 0, totalSizeBytes: 0, tables: [] },
      performance: { avgCpuPercent: 0, memoryUsedMb: 0, avgIoMbPerSecond: 0, maxDatabaseSizeMb: 0 },
      compatibilityIssues: [],
      recommendation: { serviceTier: 'Basic', computeSize: 'S0', storageGb: 5, estimatedMonthlyCostUsd: 5, reasoning: 'test' },
      compatibilityScore: 100,
      risk: 'Low',
    };
    expect(report.id).toBe('r1');
  });

  it('MigrationPlan compiles with required and optional fields', () => {
    const plan: MigrationPlan = {
      id: 'mp1',
      projectId: 'p1',
      strategy: 'Cutover',
      includedObjects: ['dbo.Users'],
      excludedObjects: [],
      targetTier: { serviceTier: 'Basic', computeSize: 'S0', storageGb: 5, estimatedMonthlyCostUsd: 5, reasoning: 'test' },
      useExistingTarget: false,
      createdAt: '2025-01-01T00:00:00Z',
      isApproved: false,
    };
    expect(plan.strategy).toBe('Cutover');
  });

  it('MigrationResult and ValidationResult compile correctly', () => {
    const validation: ValidationResult = {
      tableName: 'dbo.Users',
      sourceRowCount: 100,
      targetRowCount: 100,
      checksumMatch: true,
      passed: true,
    };
    const result: MigrationResult = {
      id: 'mr1',
      projectId: 'p1',
      success: true,
      startedAt: '2025-01-01T00:00:00Z',
      rowsMigrated: 100,
      dataSizeBytes: 5000,
      validations: [validation],
      errors: [],
    };
    expect(result.success).toBe(true);
  });

  it('MigrationProject compiles with optional nested types', () => {
    const project: MigrationProject = {
      id: 'p1',
      name: 'Test',
      status: 'Created',
      createdBy: 'test@contoso.com',
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-01T00:00:00Z',
    };
    expect(project.sourceConnection).toBeUndefined();
    expect(project.assessment).toBeUndefined();
    expect(project.migrationPlan).toBeUndefined();
  });
});
