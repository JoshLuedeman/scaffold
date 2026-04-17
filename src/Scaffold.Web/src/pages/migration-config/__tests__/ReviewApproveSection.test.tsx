import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TestWrapper } from '../../../test/msalMock';
import { ReviewApproveSection } from '../ReviewApproveSection';
import type { MigrationPlan } from '../../../types';
import type { CannedScriptInfo } from '../types';

const basePlan: MigrationPlan = {
  id: 'plan-1',
  projectId: '1',
  strategy: 'Cutover',
  includedObjects: ['dbo.Users', 'dbo.Orders'],
  excludedObjects: ['dbo.Temp'],
  targetTier: {
    serviceTier: 'General Purpose',
    computeSize: '4 vCores',
    storageGb: 32,
    estimatedMonthlyCostUsd: 300,
    reasoning: 'Good fit',
  },
  useExistingTarget: false,
  createdAt: '2025-01-17T10:00:00Z',
  isApproved: false,
  preMigrationScripts: [],
  postMigrationScripts: [],
};

const approvedPlan: MigrationPlan = {
  ...basePlan,
  isApproved: true,
  approvedBy: 'admin@contoso.com',
};

const rejectedPlan: MigrationPlan = {
  ...basePlan,
  isRejected: true,
  rejectedBy: 'reviewer@contoso.com',
  rejectionReason: 'Missing indexes need to be addressed first',
};

const mockScripts: CannedScriptInfo[] = [
  { scriptId: 'pre-1', label: 'Index Rebuild', phase: 'Pre', description: '', objectCount: 5 },
  { scriptId: 'post-1', label: 'Stats Update', phase: 'Post', description: '', objectCount: 3 },
];

describe('ReviewApproveSection', () => {
  it('renders Review & Approve card header', () => {
    render(
      <TestWrapper>
        <ReviewApproveSection
          savedPlan={basePlan}
          approving={false}
          rejecting={false}
          onApprove={vi.fn()}
          onReject={vi.fn()}
          availableScripts={[]}
          selectedScripts={new Set()}
          customScripts={[]}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Review & Approve')).toBeInTheDocument();
  });

  it('displays plan summary fields', () => {
    render(
      <TestWrapper>
        <ReviewApproveSection
          savedPlan={basePlan}
          approving={false}
          rejecting={false}
          onApprove={vi.fn()}
          onReject={vi.fn()}
          availableScripts={[]}
          selectedScripts={new Set()}
          customScripts={[]}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Strategy')).toBeInTheDocument();
    expect(screen.getByText('Cutover')).toBeInTheDocument();
    expect(screen.getByText('Objects')).toBeInTheDocument();
    expect(screen.getByText(/2 included, 1 excluded/)).toBeInTheDocument();
    expect(screen.getByText('General Purpose')).toBeInTheDocument();
  });

  it('shows "Pending approval" status for unapproved plan', () => {
    render(
      <TestWrapper>
        <ReviewApproveSection
          savedPlan={basePlan}
          approving={false}
          rejecting={false}
          onApprove={vi.fn()}
          onReject={vi.fn()}
          availableScripts={[]}
          selectedScripts={new Set()}
          customScripts={[]}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Pending approval')).toBeInTheDocument();
  });

  it('calls onApprove when Approve button is clicked', async () => {
    const onApprove = vi.fn();
    const user = userEvent.setup();
    render(
      <TestWrapper>
        <ReviewApproveSection
          savedPlan={basePlan}
          approving={false}
          rejecting={false}
          onApprove={onApprove}
          onReject={vi.fn()}
          availableScripts={[]}
          selectedScripts={new Set()}
          customScripts={[]}
        />
      </TestWrapper>,
    );

    await user.click(screen.getByRole('button', { name: 'Approve' }));
    expect(onApprove).toHaveBeenCalledTimes(1);
  });

  it('disables Approve button when plan is already approved', () => {
    render(
      <TestWrapper>
        <ReviewApproveSection
          savedPlan={approvedPlan}
          approving={false}
          rejecting={false}
          onApprove={vi.fn()}
          onReject={vi.fn()}
          availableScripts={[]}
          selectedScripts={new Set()}
          customScripts={[]}
        />
      </TestWrapper>,
    );
    expect(screen.getByRole('button', { name: /Approved/ })).toBeDisabled();
  });

  it('shows approval status when approved', () => {
    render(
      <TestWrapper>
        <ReviewApproveSection
          savedPlan={approvedPlan}
          approving={false}
          rejecting={false}
          onApprove={vi.fn()}
          onReject={vi.fn()}
          availableScripts={[]}
          selectedScripts={new Set()}
          customScripts={[]}
        />
      </TestWrapper>,
    );
    expect(screen.getByText(/Approved by admin@contoso.com/)).toBeInTheDocument();
  });

  it('opens reject dialog when Reject Plan button is clicked', async () => {
    const user = userEvent.setup();
    render(
      <TestWrapper>
        <ReviewApproveSection
          savedPlan={basePlan}
          approving={false}
          rejecting={false}
          onApprove={vi.fn()}
          onReject={vi.fn()}
          availableScripts={[]}
          selectedScripts={new Set()}
          customScripts={[]}
        />
      </TestWrapper>,
    );

    await user.click(screen.getByRole('button', { name: 'Reject Plan' }));
    expect(screen.getByText('Reject Migration Plan')).toBeInTheDocument();
    expect(screen.getByText('Rejection Reason')).toBeInTheDocument();
  });

  it('calls onReject with reason when rejection is confirmed', async () => {
    const onReject = vi.fn();
    const user = userEvent.setup();
    render(
      <TestWrapper>
        <ReviewApproveSection
          savedPlan={basePlan}
          approving={false}
          rejecting={false}
          onApprove={vi.fn()}
          onReject={onReject}
          availableScripts={[]}
          selectedScripts={new Set()}
          customScripts={[]}
        />
      </TestWrapper>,
    );

    await user.click(screen.getByRole('button', { name: 'Reject Plan' }));
    // Use fireEvent.change for the Fluent UI Textarea to avoid dialog interaction issues
    const textarea = screen.getByPlaceholderText(/Explain why/);
    fireEvent.change(textarea, { target: { value: 'Needs more testing' } });
    const confirmBtn = screen.getByRole('button', { name: 'Confirm Rejection' });
    await user.click(confirmBtn);
    expect(onReject).toHaveBeenCalledWith('Needs more testing');
  });

  it('disables Confirm Rejection when reason is empty', async () => {
    const user = userEvent.setup();
    render(
      <TestWrapper>
        <ReviewApproveSection
          savedPlan={basePlan}
          approving={false}
          rejecting={false}
          onApprove={vi.fn()}
          onReject={vi.fn()}
          availableScripts={[]}
          selectedScripts={new Set()}
          customScripts={[]}
        />
      </TestWrapper>,
    );

    await user.click(screen.getByRole('button', { name: 'Reject Plan' }));
    expect(screen.getByRole('button', { name: 'Confirm Rejection' })).toBeDisabled();
  });

  it('shows rejection banner when plan is rejected', () => {
    render(
      <TestWrapper>
        <ReviewApproveSection
          savedPlan={rejectedPlan}
          approving={false}
          rejecting={false}
          onApprove={vi.fn()}
          onReject={vi.fn()}
          availableScripts={[]}
          selectedScripts={new Set()}
          customScripts={[]}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Plan rejected')).toBeInTheDocument();
    expect(screen.getAllByText(/reviewer@contoso.com/).length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText(/Missing indexes need to be addressed first/)).toBeInTheDocument();
  });

  it('disables Reject Plan when plan is rejected', () => {
    render(
      <TestWrapper>
        <ReviewApproveSection
          savedPlan={rejectedPlan}
          approving={false}
          rejecting={false}
          onApprove={vi.fn()}
          onReject={vi.fn()}
          availableScripts={[]}
          selectedScripts={new Set()}
          customScripts={[]}
        />
      </TestWrapper>,
    );
    expect(screen.getByRole('button', { name: 'Reject Plan' })).toBeDisabled();
  });

  it('shows "Immediately on approval" when no schedule is set', () => {
    render(
      <TestWrapper>
        <ReviewApproveSection
          savedPlan={basePlan}
          approving={false}
          rejecting={false}
          onApprove={vi.fn()}
          onReject={vi.fn()}
          availableScripts={[]}
          selectedScripts={new Set()}
          customScripts={[]}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Immediately on approval')).toBeInTheDocument();
  });

  it('shows script counts when scripts are selected', () => {
    render(
      <TestWrapper>
        <ReviewApproveSection
          savedPlan={basePlan}
          approving={false}
          rejecting={false}
          onApprove={vi.fn()}
          onReject={vi.fn()}
          availableScripts={mockScripts}
          selectedScripts={new Set(['pre-1', 'post-1'])}
          customScripts={[]}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Pre-migration')).toBeInTheDocument();
    expect(screen.getByText('Post-migration')).toBeInTheDocument();
  });

  it('shows Approving... text while approving', () => {
    render(
      <TestWrapper>
        <ReviewApproveSection
          savedPlan={basePlan}
          approving={true}
          rejecting={false}
          onApprove={vi.fn()}
          onReject={vi.fn()}
          availableScripts={[]}
          selectedScripts={new Set()}
          customScripts={[]}
        />
      </TestWrapper>,
    );
    expect(screen.getByRole('button', { name: /Approving/ })).toBeDisabled();
  });
});