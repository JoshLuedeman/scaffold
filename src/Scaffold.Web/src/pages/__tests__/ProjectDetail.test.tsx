import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TestWrapper } from '../../test/msalMock';
import ProjectDetail from '../ProjectDetail';
import type { MigrationProject, MigrationResult } from '../../types';

vi.mock('../../services/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

// Mock useParams to return { id: '1' }
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useParams: () => ({ id: '1' }),
  };
});

const baseProject: MigrationProject = {
  id: '1',
  name: 'Northwind DB',
  description: 'Legacy ERP migration',
  status: 'Assessed',
  createdBy: 'admin@contoso.com',
  createdAt: '2025-01-15T10:30:00Z',
  updatedAt: '2025-01-20T14:00:00Z',
  assessment: {
    id: 'a1',
    projectId: '1',
    generatedAt: '2025-01-16T10:00:00Z',
    schema: {
      tableCount: 10,
      viewCount: 3,
      storedProcedureCount: 5,
      indexCount: 15,
      triggerCount: 2,
      objects: [],
    },
    dataProfile: { totalRowCount: 50000, totalSizeBytes: 5000000, tables: [] },
    performance: { avgCpuPercent: 15, memoryUsedMb: 1024, avgIoMbPerSecond: 10, maxDatabaseSizeMb: 500 },
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

const projectWithPlan: MigrationProject = {
  ...baseProject,
  status: 'MigrationPlanned',
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

const projectWithMigration: MigrationProject = {
  ...projectWithPlan,
  status: 'MigrationComplete',
  migrationPlan: {
    ...projectWithPlan.migrationPlan!,
    status: 'Completed',
    migrationId: 'mig-1',
  },
};

const mockResult: MigrationResult = {
  id: 'mig-1',
  projectId: '1',
  success: true,
  startedAt: '2025-01-20T10:00:00Z',
  completedAt: '2025-01-20T11:00:00Z',
  rowsMigrated: 50000,
  dataSizeBytes: 5000000,
  validations: [],
  errors: [],
};

describe('ProjectDetail', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockResolvedValue(baseProject);
  });

  it('renders project name', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );
    expect((await screen.findAllByText('Northwind DB')).length).toBeGreaterThanOrEqual(1);
  });

  it('renders project description', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );
    expect(await screen.findByText('Legacy ERP migration')).toBeInTheDocument();
  });

  it('renders status badge', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );
    expect(await screen.findByText('Assessed')).toBeInTheDocument();
  });

  it('renders workflow tabs', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );
    await screen.findAllByText('Northwind DB');
    expect(screen.getByRole('tab', { name: 'Assess' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Plan' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Execute' })).toBeInTheDocument();
  });

  it('defaults to Assess tab with Re-run Assessment button', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );
    await screen.findAllByText('Northwind DB');
    expect(screen.getByText('Re-run Assessment')).toBeInTheDocument();
  });

  it('shows Start Assessment CTA when no assessment exists', async () => {
    const { api } = await import('../../services/api');
    const noAssessment: MigrationProject = {
      ...baseProject,
      status: 'Created',
      assessment: undefined,
    };
    vi.mocked(api.get).mockResolvedValue(noAssessment);

    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );
    expect(await screen.findByText(/Start Assessment/)).toBeInTheDocument();
  });

  it('shows Plan tab content when clicked', async () => {
    const user = userEvent.setup();
    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockResolvedValue(projectWithPlan);

    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );

    await screen.findAllByText('Northwind DB');
    await user.click(screen.getByRole('tab', { name: 'Plan' }));

    await waitFor(() => {
      expect(screen.getByText('Strategy')).toBeInTheDocument();
      expect(screen.getByText('Cutover')).toBeInTheDocument();
    });
  });

  it('shows plan grid cards when plan exists', async () => {
    const user = userEvent.setup();
    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockResolvedValue(projectWithPlan);

    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );

    await screen.findAllByText('Northwind DB');
    await user.click(screen.getByRole('tab', { name: 'Plan' }));

    await waitFor(() => {
      expect(screen.getByText('Objects')).toBeInTheDocument();
      expect(screen.getByText('Target Tier')).toBeInTheDocument();
      expect(screen.getByText('Schedule')).toBeInTheDocument();
      expect(screen.getByText('Approval')).toBeInTheDocument();
    });
  });

  it('shows Approved badge on Plan tab', async () => {
    const user = userEvent.setup();
    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockResolvedValue(projectWithPlan);

    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );

    await screen.findAllByText('Northwind DB');
    await user.click(screen.getByRole('tab', { name: 'Plan' }));

    expect(await screen.findByText(/Approved/)).toBeInTheDocument();
  });

  it('shows Pending Approval badge for unapproved plan', async () => {
    const user = userEvent.setup();
    const { api } = await import('../../services/api');
    const unapprovedPlan = {
      ...projectWithPlan,
      migrationPlan: { ...projectWithPlan.migrationPlan!, isApproved: false, approvedBy: undefined },
    };
    vi.mocked(api.get).mockResolvedValue(unapprovedPlan);

    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );

    await screen.findAllByText('Northwind DB');
    await user.click(screen.getByRole('tab', { name: 'Plan' }));

    expect(await screen.findByText('Pending Approval')).toBeInTheDocument();
  });

  it('shows Configure Migration CTA on Plan tab when no plan exists', async () => {
    const user = userEvent.setup();
    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );

    await screen.findAllByText('Northwind DB');
    await user.click(screen.getByRole('tab', { name: 'Plan' }));

    expect(await screen.findByText(/Configure Migration/)).toBeInTheDocument();
  });

  it('shows Execute tab with Start Migration CTA when plan approved', async () => {
    const user = userEvent.setup();
    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockResolvedValue(projectWithPlan);

    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );

    await screen.findAllByText('Northwind DB');
    await user.click(screen.getByRole('tab', { name: 'Execute' }));

    expect(await screen.findByText(/Start Migration/)).toBeInTheDocument();
  });

  it('shows Not Ready on Execute tab when plan is not approved', async () => {
    const user = userEvent.setup();
    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );

    await screen.findAllByText('Northwind DB');
    await user.click(screen.getByRole('tab', { name: 'Execute' }));

    expect(await screen.findByText('Not Ready')).toBeInTheDocument();
  });

  it('shows migration history card on Execute tab when migration exists', async () => {
    const user = userEvent.setup();
    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockImplementation((path: string) => {
      if (path.includes('/migrations/')) return Promise.resolve(mockResult) as Promise<unknown>;
      return Promise.resolve(projectWithMigration) as Promise<unknown>;
    });

    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );

    await screen.findAllByText('Northwind DB');
    await user.click(screen.getByRole('tab', { name: 'Execute' }));

    expect(await screen.findByText('Recent Migration')).toBeInTheDocument();
    expect(screen.getByText(/View Details/)).toBeInTheDocument();
  });

  it('shows migration status badge in history card', async () => {
    const user = userEvent.setup();
    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockImplementation((path: string) => {
      if (path.includes('/migrations/')) return Promise.resolve(mockResult) as Promise<unknown>;
      return Promise.resolve(projectWithMigration) as Promise<unknown>;
    });

    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );

    await screen.findAllByText('Northwind DB');
    await user.click(screen.getByRole('tab', { name: 'Execute' }));

    expect(await screen.findByText('Completed')).toBeInTheDocument();
  });

  it('shows breadcrumb navigation', async () => {
    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );

    expect(await screen.findByText('Projects')).toBeInTheDocument();
  });

  it('shows loading spinner initially', () => {
    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );
    expect(screen.getByText(/Loading project/)).toBeInTheDocument();
  });

  it('shows error state when API fails', async () => {
    const { api } = await import('../../services/api');
    vi.mocked(api.get).mockRejectedValue(new Error('Network error'));

    render(
      <TestWrapper initialEntries={['/projects/1']}>
        <ProjectDetail />
      </TestWrapper>,
    );
    expect(await screen.findByText('Network error')).toBeInTheDocument();
  });
});