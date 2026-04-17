import { render, screen, within } from '@testing-library/react';
import { TestWrapper } from '../../test/msalMock';
import { RegionPricingSection } from '../migration-config/RegionPricingSection';
import { getCostBarColor } from '../migration-config/types';
import type { RegionPricing, TierRecommendation } from '../../types';

const mockPricing: RegionPricing[] = [
  { armRegionName: 'eastus', displayName: 'East US', estimatedMonthlyCostUsd: 100 },
  { armRegionName: 'westus', displayName: 'West US', estimatedMonthlyCostUsd: 120 },
  { armRegionName: 'westeurope', displayName: 'West Europe', estimatedMonthlyCostUsd: 150 },
];

const mockRecommendation: TierRecommendation = {
  serviceTier: 'General Purpose',
  computeSize: '4 vCores',
  storageGb: 32,
  estimatedMonthlyCostUsd: 300,
  reasoning: 'Good fit',
};

describe('RegionPricingSection', () => {
  it('renders region pricing table with cost bars', () => {
    render(
      <TestWrapper>
        <RegionPricingSection
          regionPricing={mockPricing}
          selectedRegion=""
          onRegionChange={vi.fn()}
          loadingPricing={false}
        />
      </TestWrapper>,
    );

    expect(screen.getByText('Deployment Region & Pricing')).toBeInTheDocument();
    expect(screen.getByText('East US')).toBeInTheDocument();
    expect(screen.getByText('West US')).toBeInTheDocument();
    expect(screen.getByText('West Europe')).toBeInTheDocument();

    // Cost bars should be rendered
    const barEastus = screen.getByTestId('cost-bar-eastus');
    expect(barEastus).toBeInTheDocument();
    const barWesteurope = screen.getByTestId('cost-bar-westeurope');
    expect(barWesteurope).toBeInTheDocument();
  });

  it('renders cost bars with proportional widths', () => {
    render(
      <TestWrapper>
        <RegionPricingSection
          regionPricing={mockPricing}
          selectedRegion=""
          onRegionChange={vi.fn()}
          loadingPricing={false}
        />
      </TestWrapper>,
    );

    // East US ($100) should have narrower bar than West Europe ($150)
    const barEastus = screen.getByTestId('cost-bar-eastus');
    const barWesteurope = screen.getByTestId('cost-bar-westeurope');

    // West Europe is the most expensive, so its width should be 100%
    expect(barWesteurope.style.width).toBe('100%');

    // East US ($100/$150 = 66.67%)
    const eastusWidth = parseFloat(barEastus.style.width);
    expect(eastusWidth).toBeGreaterThan(60);
    expect(eastusWidth).toBeLessThan(70);
  });

  it('shows tier comparison when service override differs from recommendation', () => {
    render(
      <TestWrapper>
        <RegionPricingSection
          regionPricing={mockPricing}
          selectedRegion="eastus"
          onRegionChange={vi.fn()}
          loadingPricing={false}
          recommendation={mockRecommendation}
          serviceOverride="Azure SQL Database Hyperscale"
          tierOverride="Hyperscale (4 vCores)"
        />
      </TestWrapper>,
    );

    expect(screen.getByTestId('tier-comparison')).toBeInTheDocument();
    expect(screen.getByText('Tier Comparison')).toBeInTheDocument();
    expect(screen.getByText('Recommended')).toBeInTheDocument();
    expect(screen.getByText('Selected')).toBeInTheDocument();
    expect(screen.getByText('General Purpose')).toBeInTheDocument();
    expect(screen.getByText('Azure SQL Database Hyperscale')).toBeInTheDocument();
  });

  it('does not show tier comparison when service override matches recommendation', () => {
    render(
      <TestWrapper>
        <RegionPricingSection
          regionPricing={mockPricing}
          selectedRegion="eastus"
          onRegionChange={vi.fn()}
          loadingPricing={false}
          recommendation={mockRecommendation}
          serviceOverride="General Purpose"
        />
      </TestWrapper>,
    );

    expect(screen.queryByTestId('tier-comparison')).not.toBeInTheDocument();
  });

  it('does not show tier comparison when no service override', () => {
    render(
      <TestWrapper>
        <RegionPricingSection
          regionPricing={mockPricing}
          selectedRegion="eastus"
          onRegionChange={vi.fn()}
          loadingPricing={false}
          recommendation={mockRecommendation}
          serviceOverride=""
        />
      </TestWrapper>,
    );

    expect(screen.queryByTestId('tier-comparison')).not.toBeInTheDocument();
  });

  it('shows total cost summary card when a region is selected', () => {
    render(
      <TestWrapper>
        <RegionPricingSection
          regionPricing={mockPricing}
          selectedRegion="eastus"
          onRegionChange={vi.fn()}
          loadingPricing={false}
        />
      </TestWrapper>,
    );

    const summary = screen.getByTestId('cost-summary');
    expect(summary).toBeInTheDocument();
    const summaryScope = within(summary);
    expect(summaryScope.getByText('Cost Summary')).toBeInTheDocument();
    // 'East US' appears in both the table and summary; verify Region label exists in summary
    expect(summary).toHaveTextContent('East US');
    // Monthly: $100.00
    expect(summaryScope.getByText('$100.00')).toBeInTheDocument();
    // Annual: $1200.00/yr
    expect(summaryScope.getByText('$1200.00/yr')).toBeInTheDocument();
  });

  it('does not show cost summary when no region is selected', () => {
    render(
      <TestWrapper>
        <RegionPricingSection
          regionPricing={mockPricing}
          selectedRegion=""
          onRegionChange={vi.fn()}
          loadingPricing={false}
        />
      </TestWrapper>,
    );

    expect(screen.queryByTestId('cost-summary')).not.toBeInTheDocument();
  });

  it('shows loading spinner when loading', () => {
    render(
      <TestWrapper>
        <RegionPricingSection
          regionPricing={[]}
          selectedRegion=""
          onRegionChange={vi.fn()}
          loadingPricing={true}
        />
      </TestWrapper>,
    );

    expect(screen.getByText(/Loading pricing/)).toBeInTheDocument();
  });

  it('shows no data message when pricing is empty', () => {
    render(
      <TestWrapper>
        <RegionPricingSection
          regionPricing={[]}
          selectedRegion=""
          onRegionChange={vi.fn()}
          loadingPricing={false}
        />
      </TestWrapper>,
    );

    expect(screen.getByText('No pricing data available')).toBeInTheDocument();
  });

  it('shows Cost column header', () => {
    render(
      <TestWrapper>
        <RegionPricingSection
          regionPricing={mockPricing}
          selectedRegion=""
          onRegionChange={vi.fn()}
          loadingPricing={false}
        />
      </TestWrapper>,
    );

    expect(screen.getByText('Cost')).toBeInTheDocument();
  });
});

describe('getCostBarColor', () => {
  it('returns green for the cheapest item', () => {
    const color = getCostBarColor(100, 100, 200);
    // At ratio 0, should be green: rgb(76, 175, 80)
    expect(color).toBe('rgb(76, 175, 80)');
  });

  it('returns amber for the most expensive item', () => {
    const color = getCostBarColor(200, 100, 200);
    // At ratio 1, should be amber: rgb(255, 152, 0)
    expect(color).toBe('rgb(255, 152, 0)');
  });

  it('returns green when all items have the same cost', () => {
    const color = getCostBarColor(100, 100, 100);
    expect(color).toBe('#4caf50');
  });

  it('returns intermediate color for mid-range cost', () => {
    const color = getCostBarColor(150, 100, 200);
    // At ratio 0.5
    expect(color).toMatch(/^rgb\(\d+, \d+, \d+\)$/);
    // r should be between 76 and 255, g between 152 and 175, b between 0 and 80
    const match = color.match(/rgb\((\d+), (\d+), (\d+)\)/);
    expect(match).not.toBeNull();
    const r = parseInt(match![1]);
    const g = parseInt(match![2]);
    const b = parseInt(match![3]);
    expect(r).toBeGreaterThan(76);
    expect(r).toBeLessThan(255);
    expect(g).toBeGreaterThanOrEqual(152);
    expect(g).toBeLessThanOrEqual(175);
    expect(b).toBeLessThan(80);
    expect(b).toBeGreaterThanOrEqual(0);
  });
});
