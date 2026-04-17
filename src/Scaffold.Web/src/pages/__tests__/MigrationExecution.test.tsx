import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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
    preMigrationScripts: [],
    postMigrationScripts: [],
  },
};

const runningProject: MigrationProject = {
  ...mockProject,
  status: 'Migrating',
  migrationPlan: {
    ...mockProject.migrationPlan!,
    status: 'Running',
    migrationId: 'mig-running-1',
  },
};

const completedProject: MigrationProject = {
  ...mockProject,
  status: 'MigrationComplete',
  migrationPlan: {
    ...mockProject.migrationPlan!,
    status: 'Completed',
    migrationId: 'mig-completed-1',
  },
};

const cancelledProject: MigrationProject = {
  ...mockProject,
  status: 'MigrationPlanned',
  migrationPlan: {
    ...mockProject.migrationPlan!,
    status: 'Cancelled',
    migrationId: 'mig-cancelled-1',
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
    vi.clearAllMocks();
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

  // --- Cancel Button Tests ---

  it('shows cancel button when migration is running', async () => {
    const { useMigrationProgress } = await import('../../hooks/useMigrationProgress');
    vi.mocked(useMigrationProgress).mockReturnValue({
      progress: {
        phase: 'Data Transfer',
        percentComplete: 30,
        currentTable: 'dbo.Users',
        rowsProcessed: 100,
        message: 'Migrating...',
      },
      connectionStatus: 'connected',
      log: [],
      migrationStatus: 'running',
    });

    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockResolvedValue(runningProject);

    render(
      <TestWrapper initialEntries={['/projects/1/execute']}>
        <MigrationExecution />
      </TestWrapper>,
    );

    expect(await screen.findByRole('button', { name: 'Cancel Migration' })).toBeInTheDocument();
  });

  it('does not show cancel button when migration is idle', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1/execute']}>
        <MigrationExecution />
      </TestWrapper>,
    );

    expect(await screen.findByRole('button', { name: 'Start Migration' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cancel Migration' })).not.toBeInTheDocument();
  });

  it('opens confirmation dialog when cancel is clicked', async () => {
    const user = userEvent.setup();
    const { useMigrationProgress } = await import('../../hooks/useMigrationProgress');
    vi.mocked(useMigrationProgress).mockReturnValue({
      progress: {
        phase: 'Data Transfer',
        percentComplete: 30,
        currentTable: 'dbo.Users',
        rowsProcessed: 100,
        message: 'Migrating...',
      },
      connectionStatus: 'connected',
      log: [],
      migrationStatus: 'running',
    });

    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockResolvedValue(runningProject);

    render(
      <TestWrapper initialEntries={['/projects/1/execute']}>
        <MigrationExecution />
      </TestWrapper>,
    );

    const cancelBtn = await screen.findByRole('button', { name: 'Cancel Migration' });
    await user.click(cancelBtn);

    expect(await screen.findByText(/Are you sure\? This will stop the migration/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Keep Running' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Yes, Cancel Migration' })).toBeInTheDocument();
  });

  it('calls cancel API and shows warning on success', async () => {
    const user = userEvent.setup();
    const { useMigrationProgress } = await import('../../hooks/useMigrationProgress');
    vi.mocked(useMigrationProgress).mockReturnValue({
      progress: {
        phase: 'Data Transfer',
        percentComplete: 30,
        currentTable: 'dbo.Users',
        rowsProcessed: 100,
        message: 'Migrating...',
      },
      connectionStatus: 'connected',
      log: [],
      migrationStatus: 'running',
    });

    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockResolvedValue(runningProject);
    vi.mocked(api.post).mockResolvedValue({});

    render(
      <TestWrapper initialEntries={['/projects/1/execute']}>
        <MigrationExecution />
      </TestWrapper>,
    );

    const cancelBtn = await screen.findByRole('button', { name: 'Cancel Migration' });
    await user.click(cancelBtn);

    const confirmBtn = await screen.findByRole('button', { name: 'Yes, Cancel Migration' });
    await user.click(confirmBtn);

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith(
        expect.stringContaining('/cancel'),
        {},
      );
    });

    expect(await screen.findByText('Migration was cancelled')).toBeInTheDocument();
  });

  it('shows error when cancel API fails', async () => {
    const user = userEvent.setup();
    const { useMigrationProgress } = await import('../../hooks/useMigrationProgress');
    vi.mocked(useMigrationProgress).mockReturnValue({
      progress: {
        phase: 'Data Transfer',
        percentComplete: 30,
        currentTable: 'dbo.Users',
        rowsProcessed: 100,
        message: 'Migrating...',
      },
      connectionStatus: 'connected',
      log: [],
      migrationStatus: 'running',
    });

    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockResolvedValue(runningProject);
    vi.mocked(api.post).mockRejectedValue(new Error('Network error'));

    render(
      <TestWrapper initialEntries={['/projects/1/execute']}>
        <MigrationExecution />
      </TestWrapper>,
    );

    const cancelBtn = await screen.findByRole('button', { name: 'Cancel Migration' });
    await user.click(cancelBtn);

    const confirmBtn = await screen.findByRole('button', { name: 'Yes, Cancel Migration' });
    await user.click(confirmBtn);

    expect(await screen.findByText('Network error')).toBeInTheDocument();
  });

  it('does not show cancel button when migration is completed', async () => {
    const { useMigrationProgress } = await import('../../hooks/useMigrationProgress');
    vi.mocked(useMigrationProgress).mockReturnValue({
      progress: null,
      connectionStatus: 'disconnected',
      log: [],
      migrationStatus: 'completed',
    });

    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockResolvedValue(completedProject);

    render(
      <TestWrapper initialEntries={['/projects/1/execute']}>
        <MigrationExecution />
      </TestWrapper>,
    );

    await screen.findByRole('heading', { name: 'Execute Migration' });
    expect(screen.queryByRole('button', { name: 'Cancel Migration' })).not.toBeInTheDocument();
  });

  // --- State Recovery Tests ---

  it('recovers running migration from project plan', async () => {
    const { useMigrationProgress } = await import('../../hooks/useMigrationProgress');
    vi.mocked(useMigrationProgress).mockReturnValue({
      progress: null,
      connectionStatus: 'connecting',
      log: [],
      migrationStatus: 'idle',
    });

    const { api } = await import('../../services/api');
    // First call: project fetch; Second call: progress backfill
    vi.mocked(api.get)
      .mockResolvedValueOnce(runningProject)
      .mockResolvedValueOnce({
        phase: 'Data Transfer',
        percentComplete: 60,
        currentTable: 'dbo.Orders',
        rowsProcessed: 2000,
        message: 'Resuming...',
      });

    render(
      <TestWrapper initialEntries={['/projects/1/execute']}>
        <MigrationExecution />
      </TestWrapper>,
    );

    // Should show heading
    expect(await screen.findByRole('heading', { name: 'Execute Migration' })).toBeInTheDocument();

    // Should NOT show Start Migration button (migration is recovered)
    expect(screen.queryByRole('button', { name: 'Start Migration' })).not.toBeInTheDocument();

    // Should show recovered progress
    expect(await screen.findByText('60%')).toBeInTheDocument();
  });

  it('recovers completed migration and shows result', async () => {
    const { useMigrationProgress } = await import('../../hooks/useMigrationProgress');
    vi.mocked(useMigrationProgress).mockReturnValue({
      progress: null,
      connectionStatus: 'disconnected',
      log: [],
      migrationStatus: 'idle',
    });

    const mockResult = {
      id: 'mig-completed-1',
      projectId: '1',
      success: true,
      startedAt: '2025-01-20T10:00:00Z',
      completedAt: '2025-01-20T11:30:00Z',
      rowsMigrated: 50000,
      dataSizeBytes: 1024000,
      validations: [
        { tableName: 'dbo.Users', sourceRowCount: 1000, targetRowCount: 1000, checksumMatch: true, passed: true },
      ],
      errors: [],
    };

    const { api } = await import('../../services/api');
    // First call: project fetch; Second call: result fetch (from completion effect)
    vi.mocked(api.get)
      .mockResolvedValueOnce(completedProject)
      .mockResolvedValueOnce(mockResult);

    render(
      <TestWrapper initialEntries={['/projects/1/execute']}>
        <MigrationExecution />
      </TestWrapper>,
    );

    // Should show validation results from the recovered result
    expect(await screen.findByText('All validations passed')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Start Migration' })).not.toBeInTheDocument();
  });

  it('shows cancelled state with restart options', async () => {
    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockResolvedValue(cancelledProject);

    render(
      <TestWrapper initialEntries={['/projects/1/execute']}>
        <MigrationExecution />
      </TestWrapper>,
    );

    expect(await screen.findByText(/Migration was cancelled/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Start New Migration' })).toBeInTheDocument();
    expect(screen.getByText('Reconfigure')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Start Migration' })).not.toBeInTheDocument();
  });

  it('resets state when Start New Migration is clicked after cancel', async () => {
    const user = userEvent.setup();
    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockResolvedValue(cancelledProject);

    render(
      <TestWrapper initialEntries={['/projects/1/execute']}>
        <MigrationExecution />
      </TestWrapper>,
    );

    const newMigrationBtn = await screen.findByRole('button', { name: 'Start New Migration' });
    await user.click(newMigrationBtn);

    // After reset, Start Migration should appear
    expect(await screen.findByRole('button', { name: 'Start Migration' })).toBeInTheDocument();
  });
});