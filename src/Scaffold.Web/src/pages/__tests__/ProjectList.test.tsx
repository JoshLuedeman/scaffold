import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import ProjectList from '../ProjectList';
import { api } from '../../services/api';
import type { MigrationProject } from '../../types';

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
    (api.get as ReturnType<typeof vi.fn>).mockResolvedValue(mockProjects);
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
    expect(createdElements.length).toBeGreaterThanOrEqual(2);
  });

  it('renders project links to detail pages', async () => {
    renderProjectList();
    await screen.findByText('Northwind DB');
    const links = screen.getAllByRole('link');
    expect(links.some((l) => l.getAttribute('href') === '/projects/proj-1')).toBe(true);
    expect(links.some((l) => l.getAttribute('href') === '/projects/proj-2')).toBe(true);
    expect(links.some((l) => l.getAttribute('href') === '/projects/proj-3')).toBe(true);
  });

  it('renders the table with correct headers', async () => {
    renderProjectList();
    await waitFor(() => {
      expect(screen.getByRole('columnheader', { name: 'Name' })).toBeInTheDocument();
    });
    expect(screen.getByRole('columnheader', { name: 'Status' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Created' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Updated' })).toBeInTheDocument();
  });
});
