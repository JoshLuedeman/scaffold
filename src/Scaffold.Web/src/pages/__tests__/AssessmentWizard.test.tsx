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
  default: () => <div data-testid="assessment-report">Mock Report</div>,
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
});
