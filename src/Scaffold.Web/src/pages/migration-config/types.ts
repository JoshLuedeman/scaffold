import type { DatabasePlatform, MigrationStrategy } from '../../types';

export interface CannedScriptInfo {
  scriptId: string;
  label: string;
  phase: string;
  description: string;
  objectCount: number;
}

export interface ServiceConfig {
  service: string;
  tiers: string[];
}

export const SQL_SERVER_SERVICES: ServiceConfig[] = [
  {
    service: 'Azure SQL Database',
    tiers: [
      'Basic (5 DTUs)',
      'Standard S0 (10 DTUs)',
      'Standard S1 (20 DTUs)',
      'Standard S2 (50 DTUs)',
      'Standard S3 (100 DTUs)',
      'Premium P1 (125 DTUs)',
      'Premium P2 (250 DTUs)',
      'Premium P4 (500 DTUs)',
      'General Purpose (2 vCores)',
      'General Purpose (4 vCores)',
      'General Purpose (8 vCores)',
      'General Purpose (16 vCores)',
      'Business Critical (4 vCores)',
      'Business Critical (8 vCores)',
      'Business Critical (16 vCores)',
    ],
  },
  {
    service: 'Azure SQL Database Hyperscale',
    tiers: [
      'Hyperscale (2 vCores)',
      'Hyperscale (4 vCores)',
      'Hyperscale (8 vCores)',
      'Hyperscale (16 vCores)',
      'Hyperscale (24 vCores)',
    ],
  },
  {
    service: 'Azure SQL Managed Instance',
    tiers: [
      'General Purpose (4 vCores)',
      'General Purpose (8 vCores)',
      'General Purpose (16 vCores)',
      'General Purpose (24 vCores)',
      'Business Critical (4 vCores)',
      'Business Critical (8 vCores)',
      'Business Critical (16 vCores)',
    ],
  },
  {
    service: 'SQL Server on Azure VM',
    tiers: [
      'Standard_D2s_v5 (2 vCPU, 8 GB)',
      'Standard_D4s_v5 (4 vCPU, 16 GB)',
      'Standard_D8s_v5 (8 vCPU, 32 GB)',
      'Standard_D16s_v5 (16 vCPU, 64 GB)',
      'Standard_E4s_v5 (4 vCPU, 32 GB)',
      'Standard_E8s_v5 (8 vCPU, 64 GB)',
      'Standard_E16s_v5 (16 vCPU, 128 GB)',
    ],
  },
];

export const POSTGRESQL_SERVICES: ServiceConfig[] = [
  {
    service: 'Azure Database for PostgreSQL - Flexible Server',
    tiers: [
      'Burstable B1ms (1 vCore, 2 GB)',
      'Burstable B2s (2 vCores, 4 GB)',
      'General Purpose D2s (2 vCores, 8 GB)',
      'General Purpose D4s (4 vCores, 16 GB)',
      'General Purpose D8s (8 vCores, 32 GB)',
      'General Purpose D16s (16 vCores, 64 GB)',
      'Memory Optimized E2s (2 vCores, 16 GB)',
      'Memory Optimized E4s (4 vCores, 32 GB)',
    ],
  },
];

export function getServicesForPlatform(platform?: DatabasePlatform): ServiceConfig[] {
  if (platform === 'PostgreSql') return POSTGRESQL_SERVICES;
  return SQL_SERVER_SERVICES;
}

export function getStrategyInfo(platform?: DatabasePlatform): Record<MigrationStrategy, { label: string; description: string }> {
  const continuousSyncDetail = platform === 'PostgreSql'
    ? 'Uses PostgreSQL Logical Replication. '
    : 'Uses SQL Server Change Tracking. ';

  return {
    Cutover: {
      label: 'Cutover',
      description:
        'One-time migration with a maintenance window. Source database is taken offline, data is migrated, then traffic switches to the target.',
    },
    ContinuousSync: {
      label: 'Continuous Sync',
      description:
        `${continuousSyncDetail}Ongoing replication keeps source and target in sync. Allows near-zero downtime cutover when ready.`,
    },
  };
}

export function objectKey(obj: { schema: string; name: string }): string {
  return `${obj.schema}.${obj.name}`;
}