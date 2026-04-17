import { render, screen } from '@testing-library/react';
import { TestWrapper } from '../../test/msalMock';
import AssessmentReport from '../AssessmentReport';
import type { AssessmentReport as Report } from '../../types';

vi.mock('../../services/api', () => ({
  api: {
    get: vi.fn().mockResolvedValue([]),
    post: vi.fn().mockResolvedValue({ compatibilityIssues: [] }),
  },
}));

function makeReport(overrides: Partial<Report> = {}): Report {
  return {
    id: 'rpt-1',
    projectId: 'proj-1',
    generatedAt: '2025-01-15T00:00:00Z',
    schema: {
      tableCount: 10,
      viewCount: 3,
      storedProcedureCount: 5,
      indexCount: 20,
      triggerCount: 2,
      objects: [],
    },
    dataProfile: {
      totalRowCount: 50000,
      totalSizeBytes: 1024 * 1024 * 100,
      tables: [],
    },
    performance: {
      avgCpuPercent: 25.5,
      memoryUsedMb: 2048,
      avgIoMbPerSecond: 10.2,
      maxDatabaseSizeMb: 500,
    },
    compatibilityIssues: [],
    recommendation: {
      serviceTier: 'Standard',
      computeSize: 'GP_S_Gen5_2',
      vCores: 2,
      storageGb: 32,
      estimatedMonthlyCostUsd: 150,
      reasoning: 'Suitable for moderate workload',
    },
    compatibilityScore: 95,
    risk: 'Low',
    ...overrides,
  };
}

describe('AssessmentReport', () => {
  describe('SQL Server (default)', () => {
    it('renders core metrics: tables, views, stored procs', () => {
      const report = makeReport();
      render(
        <TestWrapper>
          <AssessmentReport report={report} projectId="proj-1" />
        </TestWrapper>,
      );
      expect(screen.getByText('Tables')).toBeInTheDocument();
      expect(screen.getByText('10')).toBeInTheDocument();
      expect(screen.getByText('Views')).toBeInTheDocument();
      expect(screen.getByText('3')).toBeInTheDocument();
      expect(screen.getByText('Stored Procs')).toBeInTheDocument();
      expect(screen.getByText('5')).toBeInTheDocument();
    });

    it('renders tier recommendation with SQL Server label', () => {
      const report = makeReport();
      render(
        <TestWrapper>
          <AssessmentReport report={report} projectId="proj-1" platform="SqlServer" />
        </TestWrapper>,
      );
      expect(screen.getByText(/Standard — GP_S_Gen5_2/)).toBeInTheDocument();
    });

    it('renders Service Compatibility heading for SQL Server', () => {
      const report = makeReport();
      render(
        <TestWrapper>
          <AssessmentReport report={report} projectId="proj-1" platform="SqlServer" />
        </TestWrapper>,
      );
      expect(screen.getByText('Service Compatibility')).toBeInTheDocument();
    });

    it('does not show extensions or sequences for SQL Server', () => {
      const report = makeReport({
        schema: {
          tableCount: 10, viewCount: 3, storedProcedureCount: 5,
          indexCount: 20, triggerCount: 2,
          extensionCount: 4, sequenceCount: 2, objects: [],
        },
      });
      render(
        <TestWrapper>
          <AssessmentReport report={report} projectId="proj-1" platform="SqlServer" />
        </TestWrapper>,
      );
      expect(screen.queryByText('Extensions')).not.toBeInTheDocument();
      expect(screen.queryByText('Sequences')).not.toBeInTheDocument();
    });
  });

  describe('PostgreSQL', () => {
    it('shows extensions and sequences when present', () => {
      const report = makeReport({
        schema: {
          tableCount: 8, viewCount: 2, storedProcedureCount: 0,
          indexCount: 15, triggerCount: 0,
          extensionCount: 4, sequenceCount: 7, objects: [],
        },
      });
      render(
        <TestWrapper>
          <AssessmentReport report={report} projectId="proj-1" platform="PostgreSql" />
        </TestWrapper>,
      );
      expect(screen.getByText('Extensions')).toBeInTheDocument();
      expect(screen.getByText('4')).toBeInTheDocument();
      expect(screen.getByText('Sequences')).toBeInTheDocument();
      expect(screen.getByText('7')).toBeInTheDocument();
    });

    it('hides stored procs when count is 0 for PostgreSQL', () => {
      const report = makeReport({
        schema: {
          tableCount: 8, viewCount: 2, storedProcedureCount: 0,
          indexCount: 15, triggerCount: 0, objects: [],
        },
      });
      render(
        <TestWrapper>
          <AssessmentReport report={report} projectId="proj-1" platform="PostgreSql" />
        </TestWrapper>,
      );
      expect(screen.queryByText('Stored Procs')).not.toBeInTheDocument();
    });

    it('shows stored procs for PostgreSQL when count is non-zero', () => {
      const report = makeReport({
        schema: {
          tableCount: 8, viewCount: 2, storedProcedureCount: 3,
          indexCount: 15, triggerCount: 0, objects: [],
        },
      });
      render(
        <TestWrapper>
          <AssessmentReport report={report} projectId="proj-1" platform="PostgreSql" />
        </TestWrapper>,
      );
      expect(screen.getByText('Stored Procs')).toBeInTheDocument();
      expect(screen.getByText('3')).toBeInTheDocument();
    });

    it('maps Standard tier to General Purpose for PostgreSQL', () => {
      const report = makeReport({
        recommendation: {
          serviceTier: 'Standard',
          computeSize: 'GP_D2s_v3',
          vCores: 2,
          storageGb: 32,
          estimatedMonthlyCostUsd: 120,
          reasoning: 'Good for general workloads',
        },
      });
      render(
        <TestWrapper>
          <AssessmentReport report={report} projectId="proj-1" platform="PostgreSql" />
        </TestWrapper>,
      );
      expect(screen.getByText(/General Purpose — GP_D2s_v3/)).toBeInTheDocument();
    });

    it('maps Basic tier to Burstable for PostgreSQL', () => {
      const report = makeReport({
        recommendation: {
          serviceTier: 'Basic',
          computeSize: 'B1ms',
          storageGb: 5,
          estimatedMonthlyCostUsd: 25,
          reasoning: 'Light workload',
        },
      });
      render(
        <TestWrapper>
          <AssessmentReport report={report} projectId="proj-1" platform="PostgreSql" />
        </TestWrapper>,
      );
      expect(screen.getByText(/Burstable — B1ms/)).toBeInTheDocument();
    });

    it('maps Premium tier to Memory Optimized for PostgreSQL', () => {
      const report = makeReport({
        recommendation: {
          serviceTier: 'Premium',
          computeSize: 'MO_E4s_v3',
          vCores: 4,
          storageGb: 128,
          estimatedMonthlyCostUsd: 450,
          reasoning: 'Memory intensive workload',
        },
      });
      render(
        <TestWrapper>
          <AssessmentReport report={report} projectId="proj-1" platform="PostgreSql" />
        </TestWrapper>,
      );
      expect(screen.getByText(/Memory Optimized — MO_E4s_v3/)).toBeInTheDocument();
    });

    it('renders Azure PostgreSQL Service Compatibility heading', () => {
      const report = makeReport();
      render(
        <TestWrapper>
          <AssessmentReport report={report} projectId="proj-1" platform="PostgreSql" />
        </TestWrapper>,
      );
      expect(screen.getByText('Azure PostgreSQL Service Compatibility')).toBeInTheDocument();
    });
  });

  describe('performance profile', () => {
    it('renders performance metrics', () => {
      const report = makeReport();
      render(
        <TestWrapper>
          <AssessmentReport report={report} projectId="proj-1" />
        </TestWrapper>,
      );
      expect(screen.getByText('Performance Profile')).toBeInTheDocument();
      expect(screen.getByText('25.5%')).toBeInTheDocument();
      expect(screen.getByText('2.00 GB')).toBeInTheDocument();
      expect(screen.getByText('10.2 MB/s')).toBeInTheDocument();
      expect(screen.getByText('500 MB')).toBeInTheDocument();
    });
  });

  describe('compatibility issues', () => {
    it('shows no issues message when there are none', () => {
      const report = makeReport({ compatibilityIssues: [] });
      render(
        <TestWrapper>
          <AssessmentReport report={report} projectId="proj-1" />
        </TestWrapper>,
      );
      expect(screen.getByText('No compatibility issues found.')).toBeInTheDocument();
    });
  });
});
