import { render, screen } from '@testing-library/react';
import { TestWrapper } from '../../test/msalMock';
import MigrationExecution from '../MigrationExecution';
import type { MigrationProject } from '../../types';

const mockProject: MigrationProject = {
  id: '1',
  name: 'Test Project',
  status: 'MigrationPlanned',
  createdBy: 'admin@contoso.com',
  createdAt: '2025-01-15T10:30:00Z',
  updatedAt: '2025-01-20T14:00:00Z',
  migrationPlan: {
    id: 'p1',
    projectId: '1',
    strategy: 'Cutover',
    includedObjects: ['dbo.Users'],
    excludedObjects: [],
    targetTier: {
      serviceTier: 'General Purpose',
      computeSize: '4 vCores',
      storageGb: 32,
      estimatedMonthlyCostUsd: 300,
      reasoning: 'Good fit',
    },
    useExistingTarget: false,
    createdAt: '2025-01-17T10:00:00Z',
    isApproved: true,
    approvedBy: 'admin@contoso.com',
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

vi.mock('../../hooks/useMigrationProgress', () => ({
  useMigrationProgress: vi.fn().mockReturnValue({
    progress: null,
    connectionStatus: 'disconnected',
    log: [],
    migrationStatus: 'idle',
  }),
}));

describe('MigrationExecution', () => {
  beforeEach(async () => {
    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockResolvedValue(mockProject);
  });

  it('renders the Execute Migration heading', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1/execute']}>
        <MigrationExecution />
      </TestWrapper>,
    );

    expect(await screen.findByRole('heading', { name: 'Execute Migration' })).toBeInTheDocument();
  });

  it('renders the Start Migration button', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1/execute']}>
        <MigrationExecution />
      </TestWrapper>,
    );

    expect(await screen.findByRole('button', { name: 'Start Migration' })).toBeInTheDocument();
  });

  it('displays the migration strategy', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1/execute']}>
        <MigrationExecution />
      </TestWrapper>,
    );

    expect(await screen.findByText('Cutover')).toBeInTheDocument();
  });

  it('shows progress elements when migration is running', async () => {
    const { useMigrationProgress } = await import('../../hooks/useMigrationProgress');
    vi.mocked(useMigrationProgress).mockReturnValue({
      progress: {
        phase: 'Data Transfer',
        percentComplete: 45,
        currentTable: 'dbo.Users',
        rowsProcessed: 500,
        message: 'Migrating dbo.Users',
      },
      connectionStatus: 'connected',
      log: [{ timestamp: new Date(), message: 'Migration started' }],
      migrationStatus: 'running',
    });

    const { api } = await import('../../services/api');
    vi.mocked(api.post).mockResolvedValueOnce({ id: 'mig-1' });

    render(
      <TestWrapper initialEntries={['/projects/1/execute']}>
        <MigrationExecution />
      </TestWrapper>,
    );

    expect(await screen.findByRole('heading', { name: 'Execute Migration' })).toBeInTheDocument();
  });

  it('shows breadcrumb navigation', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1/execute']}>
        <MigrationExecution />
      </TestWrapper>,
    );

    expect(await screen.findByText('Projects')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Execute Migration' })).toBeInTheDocument();
  });
});
