import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TestWrapper } from '../../../test/msalMock';
import { StrategySection } from '../StrategySection';
import type { StrategyRecommendation } from '../../../types';

const mockRecommendation: StrategyRecommendation = {
  recommendedStrategy: 'ContinuousSync',
  reasoning: 'Large database with high availability needs',
  estimatedDowntimeCutover: '4-6 hours',
  estimatedDowntimeContinuousSync: '< 5 minutes',
  considerations: ['Requires Change Tracking enabled'],
};

describe('StrategySection', () => {
  it('renders strategy radio buttons without recommendation', () => {
    render(
      <TestWrapper>
        <StrategySection strategy="Cutover" onStrategyChange={vi.fn()} />
      </TestWrapper>,
    );
    expect(screen.getByText('Migration Strategy')).toBeInTheDocument();
    expect(screen.getByText('Cutover')).toBeInTheDocument();
    expect(screen.getByText('Continuous Sync')).toBeInTheDocument();
    expect(screen.queryByText(/Based on your assessment/)).not.toBeInTheDocument();
  });

  it('shows recommendation banner', () => {
    render(
      <TestWrapper>
        <StrategySection
          strategy="Cutover"
          onStrategyChange={vi.fn()}
          strategyRecommendation={mockRecommendation}
        />
      </TestWrapper>,
    );
    const banner = screen.getByText(/Based on your assessment, we recommend/);
    expect(banner).toBeInTheDocument();
    expect(banner.closest('[class*="MessageBar"]') ?? banner.parentElement).toHaveTextContent('Continuous Sync');
  });

  it('shows Recommended badge', () => {
    render(
      <TestWrapper>
        <StrategySection
          strategy="Cutover"
          onStrategyChange={vi.fn()}
          strategyRecommendation={mockRecommendation}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Recommended')).toBeInTheDocument();
  });

  it('shows estimated downtime for each strategy', () => {
    render(
      <TestWrapper>
        <StrategySection
          strategy="Cutover"
          onStrategyChange={vi.fn()}
          strategyRecommendation={mockRecommendation}
        />
      </TestWrapper>,
    );
    expect(screen.getByText(/Est\. downtime: 4-6 hours/)).toBeInTheDocument();
    expect(screen.getByText(/Est\. downtime: < 5 minutes/)).toBeInTheDocument();
  });

  it('gracefully handles undefined strategyRecommendation', () => {
    render(
      <TestWrapper>
        <StrategySection strategy="ContinuousSync" onStrategyChange={vi.fn()} />
      </TestWrapper>,
    );
    expect(screen.getByText('Migration Strategy')).toBeInTheDocument();
    expect(screen.queryByText(/Based on your assessment/)).not.toBeInTheDocument();
    expect(screen.queryByText('Recommended')).not.toBeInTheDocument();
    expect(screen.queryByText(/Est\. downtime/)).not.toBeInTheDocument();
  });

  it('shows N/A for continuous sync downtime when not provided', () => {
    const rec: StrategyRecommendation = {
      recommendedStrategy: 'Cutover',
      reasoning: 'Simple migration',
      estimatedDowntimeCutover: '1 hour',
      considerations: [],
    };
    render(
      <TestWrapper>
        <StrategySection strategy="Cutover" onStrategyChange={vi.fn()} strategyRecommendation={rec} />
      </TestWrapper>,
    );
    expect(screen.getByText(/Est\. downtime: 1 hour/)).toBeInTheDocument();
    expect(screen.getByText(/Est\. downtime: N\/A/)).toBeInTheDocument();
  });

  // Platform-conditional tests
  it('shows SQL Server descriptions by default (no platform)', () => {
    render(
      <TestWrapper>
        <StrategySection strategy="Cutover" onStrategyChange={vi.fn()} />
      </TestWrapper>,
    );
    // ContinuousSync should mention "Change Tracking" for SQL Server
    expect(screen.getByText(/Change Tracking/)).toBeInTheDocument();
  });

  it('shows PostgreSQL descriptions when sourcePlatform is PostgreSql', () => {
    render(
      <TestWrapper>
        <StrategySection strategy="Cutover" onStrategyChange={vi.fn()} sourcePlatform="PostgreSql" />
      </TestWrapper>,
    );
    // ContinuousSync should mention "Logical Replication" for PostgreSQL
    expect(screen.getByText(/Logical Replication/)).toBeInTheDocument();
  });

  it('shows SQL Server descriptions when sourcePlatform is SqlServer', () => {
    render(
      <TestWrapper>
        <StrategySection strategy="Cutover" onStrategyChange={vi.fn()} sourcePlatform="SqlServer" />
      </TestWrapper>,
    );
    expect(screen.getByText(/Change Tracking/)).toBeInTheDocument();
  });

  it('calls onStrategyChange when strategy is selected', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <TestWrapper>
        <StrategySection strategy="Cutover" onStrategyChange={onChange} />
      </TestWrapper>,
    );

    // Click on ContinuousSync radio
    const continuousSyncRadio = screen.getByRole('radio', { name: /Continuous Sync/ });
    await user.click(continuousSyncRadio);
    expect(onChange).toHaveBeenCalledWith('ContinuousSync');
  });
});