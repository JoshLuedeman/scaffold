import { render, screen, fireEvent } from '@testing-library/react';
import { TestWrapper } from '../../test/msalMock';
import { TargetConnectionSection } from '../migration-config/TargetConnectionSection';
import type { TargetConnectionSectionProps } from '../migration-config/TargetConnectionSection';

function makeProps(overrides: Partial<TargetConnectionSectionProps> = {}): TargetConnectionSectionProps {
  return {
    useExistingTarget: false,
    onUseExistingTargetChange: vi.fn(),
    targetServer: '',
    onTargetServerChange: vi.fn(),
    targetDatabase: '',
    onTargetDatabaseChange: vi.fn(),
    targetUsername: '',
    onTargetUsernameChange: vi.fn(),
    targetPassword: '',
    onTargetPasswordChange: vi.fn(),
    targetPort: '5432',
    onTargetPortChange: vi.fn(),
    targetSslMode: 'Require',
    onTargetSslModeChange: vi.fn(),
    targetAuthType: 'SQL',
    onTargetAuthTypeChange: vi.fn(),
    testingTarget: false,
    targetTestResult: null,
    onTestConnection: vi.fn(),
    ...overrides,
  };
}

describe('TargetConnectionSection', () => {
  it('renders the Target Database card header', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection {...makeProps()} />
      </TestWrapper>,
    );
    expect(screen.getByText('Target Database')).toBeInTheDocument();
  });

  it('shows PostgreSQL badge when platform is PostgreSql', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection {...makeProps({ sourcePlatform: 'PostgreSql' })} />
      </TestWrapper>,
    );
    expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
  });

  it('shows SQL Server badge when platform is SqlServer', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection {...makeProps({ sourcePlatform: 'SqlServer' })} />
      </TestWrapper>,
    );
    expect(screen.getByText('SQL Server')).toBeInTheDocument();
  });

  it('shows PostgreSQL fields when useExistingTarget is true and platform is PostgreSql', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            useExistingTarget: true,
            sourcePlatform: 'PostgreSql',
          })}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Host')).toBeInTheDocument();
    expect(screen.getByText('Port')).toBeInTheDocument();
    expect(screen.getByText('SSL Mode')).toBeInTheDocument();
    expect(screen.getByText('Username')).toBeInTheDocument();
    expect(screen.getByText('Password')).toBeInTheDocument();
  });

  it('shows SQL Server fields when useExistingTarget is true and platform is SqlServer', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            useExistingTarget: true,
            sourcePlatform: 'SqlServer',
          })}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Server')).toBeInTheDocument();
    expect(screen.getByText('Database')).toBeInTheDocument();
    expect(screen.getByText('Authentication')).toBeInTheDocument();
  });

  it('shows SQL Server username/password fields when auth type is SQL', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            useExistingTarget: true,
            sourcePlatform: 'SqlServer',
            targetAuthType: 'SQL',
          })}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Username')).toBeInTheDocument();
    expect(screen.getByText('Password')).toBeInTheDocument();
  });

  it('hides SQL Server username/password when auth type is Windows', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            useExistingTarget: true,
            sourcePlatform: 'SqlServer',
            targetAuthType: 'Windows',
          })}
        />
      </TestWrapper>,
    );
    // Username/Password should not be rendered for Windows auth
    expect(screen.queryByText('Username')).not.toBeInTheDocument();
    expect(screen.queryByText('Password')).not.toBeInTheDocument();
  });

  it('shows port validation error for non-numeric port', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            useExistingTarget: true,
            sourcePlatform: 'PostgreSql',
            targetPort: 'abc',
          })}
        />
      </TestWrapper>,
    );
    expect(screen.getByText(/Must be a valid port number/)).toBeInTheDocument();
  });

  it('does not show port validation error for valid port', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            useExistingTarget: true,
            sourcePlatform: 'PostgreSql',
            targetPort: '5432',
          })}
        />
      </TestWrapper>,
    );
    expect(screen.queryByText(/Must be a valid port number/)).not.toBeInTheDocument();
  });

  it('disables Test Connection when required PostgreSQL fields are missing', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            useExistingTarget: true,
            sourcePlatform: 'PostgreSql',
            targetServer: '',
            targetDatabase: '',
          })}
        />
      </TestWrapper>,
    );
    const btn = screen.getByRole('button', { name: 'Test Connection' });
    expect(btn).toBeDisabled();
  });

  it('enables Test Connection when all PostgreSQL fields are filled', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            useExistingTarget: true,
            sourcePlatform: 'PostgreSql',
            targetServer: 'myhost',
            targetDatabase: 'mydb',
            targetUsername: 'user',
            targetPassword: 'pass',
            targetPort: '5432',
          })}
        />
      </TestWrapper>,
    );
    const btn = screen.getByRole('button', { name: 'Test Connection' });
    expect(btn).not.toBeDisabled();
  });

  it('calls onTestConnection when button is clicked', () => {
    const onTestConnection = vi.fn();
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            useExistingTarget: true,
            sourcePlatform: 'SqlServer',
            targetServer: 'server',
            targetDatabase: 'db',
            targetUsername: 'user',
            targetPassword: 'pass',
            onTestConnection,
          })}
        />
      </TestWrapper>,
    );
    const btn = screen.getByRole('button', { name: 'Test Connection' });
    fireEvent.click(btn);
    expect(onTestConnection).toHaveBeenCalledTimes(1);
  });

  it('shows success message after successful connection test', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            useExistingTarget: true,
            sourcePlatform: 'SqlServer',
            targetServer: 'server',
            targetDatabase: 'db',
            targetUsername: 'user',
            targetPassword: 'pass',
            targetTestResult: { ok: true, message: 'Connection successful' },
          })}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Connection successful')).toBeInTheDocument();
  });

  it('shows error message after failed connection test', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            useExistingTarget: true,
            sourcePlatform: 'SqlServer',
            targetServer: 'server',
            targetDatabase: 'db',
            targetUsername: 'user',
            targetPassword: 'pass',
            targetTestResult: { ok: false, message: 'Connection refused' },
          })}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Connection refused')).toBeInTheDocument();
  });

  it('does not show form fields when useExistingTarget is false', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection {...makeProps({ sourcePlatform: 'SqlServer' })} />
      </TestWrapper>,
    );
    expect(screen.queryByText('Server')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Test Connection' })).not.toBeInTheDocument();
  });

  it('enables Test Connection for SQL Server Windows auth without username/password', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            useExistingTarget: true,
            sourcePlatform: 'SqlServer',
            targetAuthType: 'Windows',
            targetServer: 'server',
            targetDatabase: 'db',
          })}
        />
      </TestWrapper>,
    );
    const btn = screen.getByRole('button', { name: 'Test Connection' });
    expect(btn).not.toBeDisabled();
  });
});
