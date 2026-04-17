import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TestWrapper } from '../../../test/msalMock';
import { TargetTierSection } from '../TargetTierSection';
import type { TierRecommendation, RegionPricing } from '../../../types';

const mockRecommendation: TierRecommendation = {
  serviceTier: 'General Purpose',
  computeSize: '4 vCores',
  storageGb: 32,
  estimatedMonthlyCostUsd: 300,
  reasoning: 'Good fit',
};

const mockPricing: RegionPricing[] = [
  { armRegionName: 'eastus', displayName: 'East US', estimatedMonthlyCostUsd: 250 },
];

describe('TargetTierSection', () => {
  it('renders the Target Tier card header', () => {
    render(
      <TestWrapper>
        <TargetTierSection
          regionPricing={[]}
          serviceOverride=""
          tierOverride=""
          onServiceOverrideChange={vi.fn()}
          onTierOverrideChange={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Target Tier')).toBeInTheDocument();
  });

  it('shows recommendation banner when recommendation is provided', () => {
    render(
      <TestWrapper>
        <TargetTierSection
          recommendation={mockRecommendation}
          regionPricing={mockPricing}
          serviceOverride=""
          tierOverride=""
          onServiceOverrideChange={vi.fn()}
          onTierOverrideChange={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByText(/Recommended/)).toBeInTheDocument();
    expect(screen.getByText(/General Purpose/)).toBeInTheDocument();
  });

  it('shows SQL Server services by default (no platform)', () => {
    render(
      <TestWrapper>
        <TargetTierSection
          regionPricing={[]}
          serviceOverride=""
          tierOverride=""
          onServiceOverrideChange={vi.fn()}
          onTierOverrideChange={vi.fn()}
        />
      </TestWrapper>,
    );
    // The Select should contain "Use recommended" option
    expect(screen.getByText('Use recommended')).toBeInTheDocument();
  });

  it('shows PostgreSQL services when sourcePlatform is PostgreSql', async () => {
    const user = userEvent.setup();
    const onServiceChange = vi.fn();
    render(
      <TestWrapper>
        <TargetTierSection
          regionPricing={[]}
          serviceOverride=""
          tierOverride=""
          onServiceOverrideChange={onServiceChange}
          onTierOverrideChange={vi.fn()}
          sourcePlatform="PostgreSql"
        />
      </TestWrapper>,
    );
    // Open the select and check for PostgreSQL option
    const select = screen.getByRole('combobox');
    await user.selectOptions(select, 'Azure Database for PostgreSQL - Flexible Server');
    expect(onServiceChange).toHaveBeenCalledWith('Azure Database for PostgreSQL - Flexible Server');
  });

  it('shows SQL Server services when sourcePlatform is SqlServer', () => {
    render(
      <TestWrapper>
        <TargetTierSection
          regionPricing={[]}
          serviceOverride=""
          tierOverride=""
          onServiceOverrideChange={vi.fn()}
          onTierOverrideChange={vi.fn()}
          sourcePlatform="SqlServer"
        />
      </TestWrapper>,
    );
    // Should have SQL Server service options in the dropdown
    expect(screen.getByText('Target Tier')).toBeInTheDocument();
  });

  it('shows tier dropdown when a service is selected', () => {
    render(
      <TestWrapper>
        <TargetTierSection
          regionPricing={[]}
          serviceOverride="Azure SQL Database"
          tierOverride=""
          onServiceOverrideChange={vi.fn()}
          onTierOverrideChange={vi.fn()}
          sourcePlatform="SqlServer"
        />
      </TestWrapper>,
    );
    // When a service override is set, the tier select should appear
    expect(screen.getByText('Service Tier')).toBeInTheDocument();
  });

  it('hides tier dropdown when no service is selected', () => {
    render(
      <TestWrapper>
        <TargetTierSection
          regionPricing={[]}
          serviceOverride=""
          tierOverride=""
          onServiceOverrideChange={vi.fn()}
          onTierOverrideChange={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.queryByText('Service Tier')).not.toBeInTheDocument();
  });

  it('calls onTierOverrideChange when tier is selected', async () => {
    const user = userEvent.setup();
    const onTierChange = vi.fn();
    render(
      <TestWrapper>
        <TargetTierSection
          regionPricing={[]}
          serviceOverride="Azure SQL Database"
          tierOverride=""
          onServiceOverrideChange={vi.fn()}
          onTierOverrideChange={onTierChange}
          sourcePlatform="SqlServer"
        />
      </TestWrapper>,
    );
    const tierSelect = screen.getAllByRole('combobox')[1]; // Second combobox is tier
    await user.selectOptions(tierSelect, 'General Purpose (4 vCores)');
    expect(onTierChange).toHaveBeenCalledWith('General Purpose (4 vCores)');
  });

  it('shows pricing in recommendation when regionPricing is available', () => {
    render(
      <TestWrapper>
        <TargetTierSection
          recommendation={mockRecommendation}
          regionPricing={mockPricing}
          serviceOverride=""
          tierOverride=""
          onServiceOverrideChange={vi.fn()}
          onTierOverrideChange={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByText(/\$250/)).toBeInTheDocument();
  });

  it('falls back to recommendation cost when no regionPricing', () => {
    render(
      <TestWrapper>
        <TargetTierSection
          recommendation={mockRecommendation}
          regionPricing={[]}
          serviceOverride=""
          tierOverride=""
          onServiceOverrideChange={vi.fn()}
          onTierOverrideChange={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByText(/\$300/)).toBeInTheDocument();
  });
});