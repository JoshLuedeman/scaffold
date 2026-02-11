import { render, screen, waitFor } from '@testing-library/react';
import { TestWrapper } from '../../test/msalMock';
import MigrationConfig from '../MigrationConfig';
import type { MigrationProject } from '../../types';

const mockProject: MigrationProject = {
  id: '1',
  name: 'Test Project',
  status: 'Assessed',
  createdBy: 'admin@contoso.com',
  createdAt: '2025-01-15T10:30:00Z',
  updatedAt: '2025-01-20T14:00:00Z',
  assessment: {
    id: 'a1',
    projectId: '1',
    generatedAt: '2025-01-16T10:00:00Z',
    schema: {
      tableCount: 2,
      viewCount: 0,
      storedProcedureCount: 0,
      indexCount: 0,
      triggerCount: 0,
      objects: [
        { name: 'Users', schema: 'dbo', objectType: 'Table' },
        { name: 'Orders', schema: 'dbo', objectType: 'Table' },
      ],
    },
    dataProfile: { totalRowCount: 1000, totalSizeBytes: 50000, tables: [] },
    performance: { avgCpuPercent: 10, memoryUsedMb: 512, avgIoMbPerSecond: 5, maxDatabaseSizeMb: 100 },
    compatibilityIssues: [],
    recommendation: {
      serviceTier: 'General Purpose',
      computeSize: '4 vCores',
      storageGb: 32,
      estimatedMonthlyCostUsd: 300,
      reasoning: 'Good fit',
    },
    compatibilityScore: 95,
    risk: 'Low',
  },
};

vi.mock('../../services/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

describe('MigrationConfig', () => {
  beforeEach(async () => {
    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockResolvedValue(mockProject);
  });

  it('renders the strategy selection with Cutover and Continuous Sync', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1/configure']}>
        <MigrationConfig />
      </TestWrapper>,
    );

    expect(await screen.findByText('Migration Strategy')).toBeInTheDocument();
    expect(screen.getByText('Cutover')).toBeInTheDocument();
    expect(screen.getByText('Continuous Sync')).toBeInTheDocument();
  });

  it('renders strategy radio buttons', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1/configure']}>
        <MigrationConfig />
      </TestWrapper>,
    );

    await waitFor(() => {
      expect(screen.getByText('Migration Strategy')).toBeInTheDocument();
    });

    const radios = screen.getAllByRole('radio', { name: /Cutover|Continuous Sync/i });
    expect(radios.length).toBe(2);
  });

  it('renders schedule options', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1/configure']}>
        <MigrationConfig />
      </TestWrapper>,
    );

    expect(await screen.findByText('Schedule')).toBeInTheDocument();
    expect(screen.getByText(/Migrate now/)).toBeInTheDocument();
    expect(screen.getByText(/Schedule for/)).toBeInTheDocument();
  });

  it('renders the Save Plan button', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1/configure']}>
        <MigrationConfig />
      </TestWrapper>,
    );

    expect(await screen.findByRole('button', { name: 'Save Plan' })).toBeInTheDocument();
  });

  it('renders schema objects from assessment', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1/configure']}>
        <MigrationConfig />
      </TestWrapper>,
    );

    expect(await screen.findByText('dbo.Users')).toBeInTheDocument();
    expect(screen.getByText('dbo.Orders')).toBeInTheDocument();
  });

  it('shows object count summary', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1/configure']}>
        <MigrationConfig />
      </TestWrapper>,
    );

    expect(await screen.findByText(/2 of 2 objects selected/)).toBeInTheDocument();
  });
});
