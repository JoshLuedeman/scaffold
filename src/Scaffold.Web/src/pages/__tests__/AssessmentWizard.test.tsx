import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TestWrapper } from '../../test/msalMock';
import AssessmentWizard from '../AssessmentWizard';

vi.mock('../../services/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock('../../components/AssessmentReport', () => ({
  default: ({ platform }: { platform?: string }) => (
    <div data-testid="assessment-report" data-platform={platform}>Mock Report</div>
  ),
}));

describe('AssessmentWizard', () => {
  it('renders the connection form on step 1', () => {
    render(
      <TestWrapper initialEntries={['/projects/1/assess']}>
        <AssessmentWizard />
      </TestWrapper>,
    );
    expect(screen.getByText('Source Database Connection')).toBeInTheDocument();
  });

  it('shows Server, Database, and Port fields', () => {
    render(
      <TestWrapper initialEntries={['/projects/1/assess']}>
        <AssessmentWizard />
      </TestWrapper>,
    );
    expect(screen.getByText('Server')).toBeInTheDocument();
    expect(screen.getByText('Database')).toBeInTheDocument();
    expect(screen.getByText('Port')).toBeInTheDocument();
  });

  it('renders the Test Connection button', () => {
    render(
      <TestWrapper initialEntries={['/projects/1/assess']}>
        <AssessmentWizard />
      </TestWrapper>,
    );
    expect(screen.getByRole('button', { name: 'Test Connection' })).toBeInTheDocument();
  });

  it('disables Test Connection when server and database are empty', () => {
    render(
      <TestWrapper initialEntries={['/projects/1/assess']}>
        <AssessmentWizard />
      </TestWrapper>,
    );
    expect(screen.getByRole('button', { name: 'Test Connection' })).toBeDisabled();
  });

  it('enables Test Connection after filling server and database', async () => {
    const user = userEvent.setup();
    render(
      <TestWrapper initialEntries={['/projects/1/assess']}>
        <AssessmentWizard />
      </TestWrapper>,
    );

    await user.type(screen.getByPlaceholderText(/myserver/), 'testserver.database.windows.net');
    await user.type(screen.getByPlaceholderText(/MyDatabase/), 'TestDB');

    expect(screen.getByRole('button', { name: 'Test Connection' })).toBeEnabled();
  });

  it('advances to assess step after successful test and clicking Next', async () => {
    const { api } = await import('../../services/api');
    vi.mocked(api.post).mockResolvedValueOnce({});

    const user = userEvent.setup();
    render(
      <TestWrapper initialEntries={['/projects/1/assess']}>
        <AssessmentWizard />
      </TestWrapper>,
    );

    await user.type(screen.getByPlaceholderText(/myserver/), 'testserver');
    await user.type(screen.getByPlaceholderText(/MyDatabase/), 'TestDB');
    await user.click(screen.getByRole('button', { name: 'Test Connection' }));

    expect(await screen.findByText('Connection successful')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Next/ }));
    expect(screen.getByRole('button', { name: 'Run Assessment' })).toBeInTheDocument();
  });

  it('renders the wizard step indicators', () => {
    render(
      <TestWrapper initialEntries={['/projects/1/assess']}>
        <AssessmentWizard />
      </TestWrapper>,
    );
    expect(screen.getByText('Connect')).toBeInTheDocument();
    expect(screen.getByText('Assess')).toBeInTheDocument();
    expect(screen.getByText('Review')).toBeInTheDocument();
  });

  it('renders platform selector with SQL Server and PostgreSQL options', () => {
    render(
      <TestWrapper initialEntries={['/projects/1/assess']}>
        <AssessmentWizard />
      </TestWrapper>,
    );
    expect(screen.getByText('Database Platform')).toBeInTheDocument();
    expect(screen.getByLabelText('SQL Server')).toBeInTheDocument();
    expect(screen.getByLabelText('PostgreSQL')).toBeInTheDocument();
  });

  it('defaults to SQL Server with port 1433 and Server label', () => {
    render(
      <TestWrapper initialEntries={['/projects/1/assess']}>
        <AssessmentWizard />
      </TestWrapper>,
    );
    expect(screen.getByLabelText('SQL Server')).toBeChecked();
    expect(screen.getByText('Server')).toBeInTheDocument();
    expect(screen.getByDisplayValue('1433')).toBeInTheDocument();
  });

  it('switches labels and port when PostgreSQL is selected', async () => {
    const user = userEvent.setup();
    render(
      <TestWrapper initialEntries={['/projects/1/assess']}>
        <AssessmentWizard />
      </TestWrapper>,
    );

    await user.click(screen.getByLabelText('PostgreSQL'));

    expect(screen.getByText('Host')).toBeInTheDocument();
    expect(screen.getByDisplayValue('5432')).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/myhost\.postgres/)).toBeInTheDocument();
  });

  it('shows Password Authentication label when PostgreSQL is selected', async () => {
    const user = userEvent.setup();
    render(
      <TestWrapper initialEntries={['/projects/1/assess']}>
        <AssessmentWizard />
      </TestWrapper>,
    );

    // Default is SQL Authentication
    expect(screen.getByText('SQL Authentication')).toBeInTheDocument();

    await user.click(screen.getByLabelText('PostgreSQL'));
    expect(screen.getByText('Password Authentication')).toBeInTheDocument();
  });

  it('reverts to SQL Server defaults when switching back', async () => {
    const user = userEvent.setup();
    render(
      <TestWrapper initialEntries={['/projects/1/assess']}>
        <AssessmentWizard />
      </TestWrapper>,
    );

    // Switch to PostgreSQL
    await user.click(screen.getByLabelText('PostgreSQL'));
    expect(screen.getByDisplayValue('5432')).toBeInTheDocument();

    // Switch back to SQL Server
    await user.click(screen.getByLabelText('SQL Server'));
    expect(screen.getByDisplayValue('1433')).toBeInTheDocument();
    expect(screen.getByText('Server')).toBeInTheDocument();
  });

  it('includes platform in test connection request', async () => {
    const { api } = await import('../../services/api');
    vi.mocked(api.post).mockResolvedValueOnce({});

    const user = userEvent.setup();
    render(
      <TestWrapper initialEntries={['/projects/1/assess']}>
        <AssessmentWizard />
      </TestWrapper>,
    );

    await user.type(screen.getByPlaceholderText(/myserver/), 'testserver');
    await user.type(screen.getByPlaceholderText(/MyDatabase/), 'TestDB');
    await user.click(screen.getByRole('button', { name: 'Test Connection' }));

    expect(vi.mocked(api.post)).toHaveBeenCalledWith(
      '/connections/test',
      expect.objectContaining({ platform: 'SqlServer' }),
    );
  });

  it('sends PostgreSql platform in connection test after switching', async () => {
    const { api } = await import('../../services/api');
    vi.mocked(api.post).mockResolvedValueOnce({});

    const user = userEvent.setup();
    render(
      <TestWrapper initialEntries={['/projects/1/assess']}>
        <AssessmentWizard />
      </TestWrapper>,
    );

    await user.click(screen.getByLabelText('PostgreSQL'));
    await user.type(screen.getByPlaceholderText(/myhost\.postgres/), 'pghost');
    await user.type(screen.getByPlaceholderText(/MyDatabase/), 'TestDB');
    await user.click(screen.getByRole('button', { name: 'Test Connection' }));

    expect(vi.mocked(api.post)).toHaveBeenCalledWith(
      '/connections/test',
      expect.objectContaining({ platform: 'PostgreSql' }),
    );
  });

  it('shows Azure Database for PostgreSQL text on assess step when PostgreSQL selected', async () => {
    const { api } = await import('../../services/api');
    vi.mocked(api.post).mockResolvedValueOnce({});

    const user = userEvent.setup();
    render(
      <TestWrapper initialEntries={['/projects/1/assess']}>
        <AssessmentWizard />
      </TestWrapper>,
    );

    await user.click(screen.getByLabelText('PostgreSQL'));
    await user.type(screen.getByPlaceholderText(/myhost\.postgres/), 'pghost');
    await user.type(screen.getByPlaceholderText(/MyDatabase/), 'TestDB');
    await user.click(screen.getByRole('button', { name: 'Test Connection' }));
    await screen.findByText('Connection successful');
    await user.click(screen.getByRole('button', { name: /Next/ }));

    expect(screen.getByText(/Azure Database for PostgreSQL/)).toBeInTheDocument();
  });
});
