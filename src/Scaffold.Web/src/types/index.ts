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

export type CompatibilitySeverity = 'Supported' | 'Partial' | 'Unsupported';

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
  parentObjectName?: string;
  subType?: string;
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
  severity: CompatibilitySeverity;
  docUrl?: string;
}

export interface RegionPricing {
  armRegionName: string;
  displayName: string;
  estimatedMonthlyCostUsd: number;
}

export interface TierRecommendation {
  serviceTier: string;
  computeSize: string;
  dtus?: number;
  vCores?: number;
  storageGb: number;
  estimatedMonthlyCostUsd: number;
  reasoning: string;
  recommendedRegion?: string;
  regionalPricing?: RegionPricing[];
}

export interface ServiceCompatibility {
  service: string;
  compatibilityScore: number;
  risk: RiskRating;
  unsupportedCount: number;
  partialCount: number;
  supportedCount: number;
  totalIssues: number;
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

export type MigrationScriptType = 'Canned' | 'Custom';
export type MigrationScriptPhase = 'Pre' | 'Post';

export interface MigrationScript {
  scriptId: string;
  label: string;
  scriptType: MigrationScriptType;
  phase: MigrationScriptPhase;
  sqlContent: string;
  isEnabled: boolean;
  order: number;
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
  preMigrationScripts: MigrationScript[];
  postMigrationScripts: MigrationScript[];
  targetTier: TierRecommendation;
  useExistingTarget: boolean;
  existingTargetConnectionString?: string;
  targetRegion?: string;
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
