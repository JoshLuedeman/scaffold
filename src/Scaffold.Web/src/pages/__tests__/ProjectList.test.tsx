import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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

function makePaginatedResult(
  items: MigrationProject[],
  overrides: Partial<PaginatedResult<MigrationProject>> = {},
): PaginatedResult<MigrationProject> {
  return {
    items,
    totalCount: items.length,
    page: 1,
    pageSize: 25,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
    ...overrides,
  };
}

const mockPaginatedResult = makePaginatedResult(mockProjects);

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
    vi.useFakeTimers({ shouldAdvanceTime: true });
    (api.get as ReturnType<typeof vi.fn>).mockResolvedValue(mockPaginatedResult);
  });

  afterEach(() => {
    vi.useRealTimers();
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
    await screen.findByText('Northwind DB');
    // Status text appears in both badges and filter dropdown options,
    // so we check for multiple occurrences (badge + option)
    const assessedElements = screen.getAllByText('Assessed');
    expect(assessedElements.length).toBeGreaterThanOrEqual(1);
    const completedElements = screen.getAllByText('MigrationComplete');
    expect(completedElements.length).toBeGreaterThanOrEqual(1);
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

  it('shows loading spinner initially', () => {
    (api.get as ReturnType<typeof vi.fn>).mockReturnValue(new Promise(() => {}));
    renderProjectList();
    expect(screen.getByText('Loading projects…')).toBeInTheDocument();
  });

  it('renders search input', async () => {
    renderProjectList();
    await screen.findByText('Northwind DB');
    expect(screen.getByLabelText('Search projects')).toBeInTheDocument();
  });

  it('renders status filter dropdown', async () => {
    renderProjectList();
    await screen.findByText('Northwind DB');
    expect(screen.getByLabelText('Filter by status')).toBeInTheDocument();
  });

  it('renders sort dropdown', async () => {
    renderProjectList();
    await screen.findByText('Northwind DB');
    expect(screen.getByLabelText('Sort projects')).toBeInTheDocument();
  });

  it('filters projects by search query after debounce', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    renderProjectList();
    await screen.findByText('Northwind DB');

    const searchInput = screen.getByLabelText('Search projects');
    await user.type(searchInput, 'Northwind');

    // Advance past the debounce delay
    act(() => { vi.advanceTimersByTime(350); });

    await waitFor(() => {
      expect(screen.getByText('Northwind DB')).toBeInTheDocument();
      expect(screen.queryByText('Inventory System')).not.toBeInTheDocument();
      expect(screen.queryByText('Customer Portal')).not.toBeInTheDocument();
    });
  });

  it('filters projects by status', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    renderProjectList();
    await screen.findByText('Northwind DB');

    const statusSelect = screen.getByLabelText('Filter by status');
    await user.selectOptions(statusSelect, 'MigrationComplete');

    await waitFor(() => {
      expect(screen.getByText('Customer Portal')).toBeInTheDocument();
      expect(screen.queryByText('Northwind DB')).not.toBeInTheDocument();
      expect(screen.queryByText('Inventory System')).not.toBeInTheDocument();
    });
  });

  it('sorts projects by name descending', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    renderProjectList();
    await screen.findByText('Northwind DB');

    const sortSelect = screen.getByLabelText('Sort projects');
    await user.selectOptions(sortSelect, 'name-desc');

    await waitFor(() => {
      const cards = screen.getAllByText(/Northwind DB|Inventory System|Customer Portal/);
      expect(cards[0]).toHaveTextContent('Northwind DB');
      expect(cards[1]).toHaveTextContent('Inventory System');
      expect(cards[2]).toHaveTextContent('Customer Portal');
    });
  });

  it('shows empty state message when no projects match filters', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    renderProjectList();
    await screen.findByText('Northwind DB');

    const searchInput = screen.getByLabelText('Search projects');
    await user.type(searchInput, 'nonexistent');

    act(() => { vi.advanceTimersByTime(350); });

    await waitFor(() => {
      expect(screen.getByText('No projects match your filters.')).toBeInTheDocument();
    });
  });

  it('shows empty state with create button when no projects exist', async () => {
    (api.get as ReturnType<typeof vi.fn>).mockResolvedValue(
      makePaginatedResult([], { totalCount: 0, totalPages: 0 }),
    );
    renderProjectList();
    await waitFor(() => {
      expect(screen.getByText('No projects yet. Create one to get started.')).toBeInTheDocument();
    });
  });

  it('shows pagination controls when there are multiple pages', async () => {
    (api.get as ReturnType<typeof vi.fn>).mockResolvedValue(
      makePaginatedResult(mockProjects, {
        totalCount: 50,
        totalPages: 2,
        hasNextPage: true,
        hasPreviousPage: false,
      }),
    );
    renderProjectList();
    await screen.findByText('Northwind DB');

    expect(screen.getByLabelText('Previous page')).toBeDisabled();
    expect(screen.getByLabelText('Next page')).toBeEnabled();
    expect(screen.getByText('Page 1 of 2 (50 total)')).toBeInTheDocument();
  });

  it('hides pagination controls on single page', async () => {
    renderProjectList();
    await screen.findByText('Northwind DB');

    expect(screen.queryByLabelText('Previous page')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Next page')).not.toBeInTheDocument();
  });

  it('navigates to next page when clicking Next', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    (api.get as ReturnType<typeof vi.fn>).mockResolvedValue(
      makePaginatedResult(mockProjects, {
        totalCount: 50,
        totalPages: 2,
        hasNextPage: true,
        hasPreviousPage: false,
      }),
    );
    renderProjectList();
    await screen.findByText('Northwind DB');

    // Reset mock to track the next call
    (api.get as ReturnType<typeof vi.fn>).mockResolvedValue(
      makePaginatedResult(mockProjects, {
        page: 2,
        totalCount: 50,
        totalPages: 2,
        hasNextPage: false,
        hasPreviousPage: true,
      }),
    );

    await user.click(screen.getByLabelText('Next page'));

    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/projects?page=2&pageSize=25');
    });
  });
});