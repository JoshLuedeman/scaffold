import { render, screen } from '@testing-library/react';
import { TestWrapper } from '../../test/msalMock';
import { StrategySection } from '../migration-config/StrategySection';
import type { StrategyRecommendation } from '../../types';

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
        <StrategySection
          strategy="Cutover"
          onStrategyChange={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Migration Strategy')).toBeInTheDocument();
    expect(screen.getByText('Cutover')).toBeInTheDocument();
    expect(screen.getByText('Continuous Sync')).toBeInTheDocument();
    // No recommendation banner
    expect(screen.queryByText(/Based on your assessment/)).not.toBeInTheDocument();
  });

  it('shows recommendation banner when strategyRecommendation is provided', () => {
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
    // The banner should mention the recommended strategy
    expect(banner.closest('[class*="MessageBar"]') ?? banner.parentElement).toHaveTextContent('Continuous Sync');
  });

  it('shows Recommended badge next to the recommended strategy', () => {
    render(
      <TestWrapper>
        <StrategySection
          strategy="Cutover"
          onStrategyChange={vi.fn()}
          strategyRecommendation={mockRecommendation}
        />
      </TestWrapper>,
    );
    // The "Recommended" badge should appear
    expect(screen.getByText('Recommended')).toBeInTheDocument();
  });

  it('shows estimated downtime for each strategy option', () => {
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
        <StrategySection
          strategy="ContinuousSync"
          onStrategyChange={vi.fn()}
          strategyRecommendation={undefined}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Migration Strategy')).toBeInTheDocument();
    expect(screen.queryByText(/Based on your assessment/)).not.toBeInTheDocument();
    expect(screen.queryByText('Recommended')).not.toBeInTheDocument();
    // Downtime hints should not appear
    expect(screen.queryByText(/Est\. downtime/)).not.toBeInTheDocument();
  });

  it('shows N/A for continuous sync downtime when not provided', () => {
    const recWithoutSync: StrategyRecommendation = {
      recommendedStrategy: 'Cutover',
      reasoning: 'Simple migration',
      estimatedDowntimeCutover: '1 hour',
      considerations: [],
    };
    render(
      <TestWrapper>
        <StrategySection
          strategy="Cutover"
          onStrategyChange={vi.fn()}
          strategyRecommendation={recWithoutSync}
        />
      </TestWrapper>,
    );
    expect(screen.getByText(/Est\. downtime: 1 hour/)).toBeInTheDocument();
    expect(screen.getByText(/Est\. downtime: N\/A/)).toBeInTheDocument();
  });
});
