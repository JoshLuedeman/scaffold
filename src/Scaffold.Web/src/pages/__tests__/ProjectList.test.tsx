import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import ProjectList from '../ProjectList';
import { api } from '../../services/api';
import type { MigrationProject, PaginatedResult } from '../../types';

vi.mock('../../services/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
  },
}));

const mockProjects: MigrationProject[] = [
  {
    id: 'proj-1',
    name: 'Northwind DB',
    description: 'Legacy ERP database migration',
    status: 'Assessed',
    createdBy: 'admin@contoso.com',
    createdAt: '2025-01-15T10:30:00Z',
    updatedAt: '2025-01-20T14:00:00Z',
  },
  {
    id: 'proj-2',
    name: 'Inventory System',
    description: 'Warehouse inventory SQL Server',
    status: 'Created',
    createdBy: 'admin@contoso.com',
    createdAt: '2025-02-01T09:00:00Z',
    updatedAt: '2025-02-01T09:00:00Z',
  },
  {
    id: 'proj-3',
    name: 'Customer Portal',
    description: 'Customer-facing app database',
    status: 'MigrationComplete',
    createdBy: 'admin@contoso.com',
    createdAt: '2024-11-05T08:00:00Z',
    updatedAt: '2025-01-10T16:45:00Z',
  },
];

const mockPaginatedResult: PaginatedResult<MigrationProject> = {
  items: mockProjects,
  totalCount: 3,
  page: 1,
  pageSize: 25,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
};

function renderProjectList() {
  return render(
    <FluentProvider theme={webLightTheme}>
      <MemoryRouter>
        <ProjectList />
      </MemoryRouter>
    </FluentProvider>,
  );
}

describe('ProjectList', () => {
  beforeEach(() => {
    (api.get as ReturnType<typeof vi.fn>).mockResolvedValue(mockPaginatedResult);
  });

  it('renders the project list heading', async () => {
    renderProjectList();
    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'Projects' })).toBeInTheDocument();
    });
  });

  it('displays mock project names', async () => {
    renderProjectList();
    expect(await screen.findByText('Northwind DB')).toBeInTheDocument();
    expect(screen.getByText('Inventory System')).toBeInTheDocument();
    expect(screen.getByText('Customer Portal')).toBeInTheDocument();
  });

  it('displays project status badges', async () => {
    renderProjectList();
    expect(await screen.findByText('Assessed')).toBeInTheDocument();
    expect(screen.getByText('MigrationComplete')).toBeInTheDocument();
    const createdElements = screen.getAllByText('Created');
    expect(createdElements.length).toBeGreaterThanOrEqual(1);
  });

  it('calls the API with pagination parameters', async () => {
    renderProjectList();
    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/projects?page=1&pageSize=25');
    });
  });

  it('renders delete buttons for each project', async () => {
    renderProjectList();
    await screen.findByText('Northwind DB');
    expect(screen.getByLabelText('Delete Northwind DB')).toBeInTheDocument();
    expect(screen.getByLabelText('Delete Inventory System')).toBeInTheDocument();
    expect(screen.getByLabelText('Delete Customer Portal')).toBeInTheDocument();
  });
});