export type ProjectStatus =
  | 'Created'
  | 'Assessing'
  | 'Assessed'
  | 'PlanningMigration'
  | 'MigrationPlanned'
  | 'Migrating'
  | 'MigrationComplete'
  | 'Failed';

export type MigrationStrategy = 'Cutover' | 'ContinuousSync';

export type RiskRating = 'Low' | 'Medium' | 'High';

export interface ConnectionInfo {
  id: string;
  server: string;
  database: string;
  port: number;
  useSqlAuthentication: boolean;
  username?: string;
  keyVaultSecretUri?: string;
  trustServerCertificate: boolean;
}

export interface SchemaObject {
  name: string;
  schema: string;
  objectType: string;
}

export interface SchemaInventory {
  tableCount: number;
  viewCount: number;
  storedProcedureCount: number;
  indexCount: number;
  triggerCount: number;
  objects: SchemaObject[];
}

export interface TableProfile {
  schemaName: string;
  tableName: string;
  rowCount: number;
  sizeBytes: number;
}

export interface DataProfile {
  totalRowCount: number;
  totalSizeBytes: number;
  tables: TableProfile[];
}

export interface PerformanceProfile {
  avgCpuPercent: number;
  memoryUsedMb: number;
  avgIoMbPerSecond: number;
  maxDatabaseSizeMb: number;
}

export interface CompatibilityIssue {
  objectName: string;
  issueType: string;
  description: string;
  isBlocking: boolean;
}

export interface TierRecommendation {
  serviceTier: string;
  computeSize: string;
  dtus?: number;
  vCores?: number;
  storageGb: number;
  estimatedMonthlyCostUsd: number;
  reasoning: string;
}

export interface AssessmentReport {
  id: string;
  projectId: string;
  generatedAt: string;
  schema: SchemaInventory;
  dataProfile: DataProfile;
  performance: PerformanceProfile;
  compatibilityIssues: CompatibilityIssue[];
  recommendation: TierRecommendation;
  compatibilityScore: number;
  risk: RiskRating;
}

export interface MigrationPlan {
  id: string;
  projectId: string;
  strategy: MigrationStrategy;
  includedObjects: string[];
  excludedObjects: string[];
  scheduledAt?: string;
  preMigrationScript?: string;
  postMigrationScript?: string;
  targetTier: TierRecommendation;
  useExistingTarget: boolean;
  existingTargetConnectionString?: string;
  createdAt: string;
  isApproved: boolean;
  approvedBy?: string;
}

export interface ValidationResult {
  tableName: string;
  sourceRowCount: number;
  targetRowCount: number;
  checksumMatch: boolean;
  passed: boolean;
}

export interface MigrationResult {
  id: string;
  projectId: string;
  success: boolean;
  startedAt: string;
  completedAt?: string;
  rowsMigrated: number;
  dataSizeBytes: number;
  validations: ValidationResult[];
  errors: string[];
}

export interface MigrationProject {
  id: string;
  name: string;
  description?: string;
  status: ProjectStatus;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
  sourceConnection?: ConnectionInfo;
  assessment?: AssessmentReport;
  migrationPlan?: MigrationPlan;
}
