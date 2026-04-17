import { render, screen, fireEvent } from '@testing-library/react';
import { TestWrapper } from '../../../test/msalMock';
import { TargetConnectionSection } from '../TargetConnectionSection';
import type { TargetConnectionSectionProps } from '../TargetConnectionSection';

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

describe('TargetConnectionSection (migration-config)', () => {
  it('shows PostgreSQL-specific checkbox label', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection {...makeProps({ sourcePlatform: 'PostgreSql' })} />
      </TestWrapper>,
    );
    expect(
      screen.getByText(/Use an existing Azure Database for PostgreSQL/),
    ).toBeInTheDocument();
  });

  it('shows SQL Server-specific checkbox label', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection {...makeProps({ sourcePlatform: 'SqlServer' })} />
      </TestWrapper>,
    );
    expect(
      screen.getByText(/Use an existing Azure SQL database/),
    ).toBeInTheDocument();
  });

  it('shows port validation error for port 0', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            useExistingTarget: true,
            sourcePlatform: 'PostgreSql',
            targetPort: '0',
          })}
        />
      </TestWrapper>,
    );
    expect(screen.getByText(/Must be a valid port number/)).toBeInTheDocument();
  });

  it('shows port validation error for port > 65535', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            useExistingTarget: true,
            sourcePlatform: 'PostgreSql',
            targetPort: '99999',
          })}
        />
      </TestWrapper>,
    );
    expect(screen.getByText(/Must be a valid port number/)).toBeInTheDocument();
  });

  it('accepts empty port without error (port is optional in display)', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            useExistingTarget: true,
            sourcePlatform: 'PostgreSql',
            targetPort: '',
          })}
        />
      </TestWrapper>,
    );
    expect(screen.queryByText(/Must be a valid port number/)).not.toBeInTheDocument();
  });

  it('shows "Testing..." text during connection test', () => {
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
            testingTarget: true,
          })}
        />
      </TestWrapper>,
    );
    expect(screen.getByRole('button', { name: /Testing/ })).toBeDisabled();
  });

  it('uses targetPlatform over sourcePlatform when both provided', () => {
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            useExistingTarget: true,
            sourcePlatform: 'SqlServer',
            targetPlatform: 'PostgreSql',
          })}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
    expect(screen.getByText('Host')).toBeInTheDocument();
  });

  it('calls onUseExistingTargetChange when checkbox toggled', () => {
    const onToggle = vi.fn();
    render(
      <TestWrapper>
        <TargetConnectionSection
          {...makeProps({
            sourcePlatform: 'SqlServer',
            onUseExistingTargetChange: onToggle,
          })}
        />
      </TestWrapper>,
    );
    const checkbox = screen.getByText(/Use an existing Azure SQL database/);
    fireEvent.click(checkbox);
    expect(onToggle).toHaveBeenCalledWith(true);
  });
});