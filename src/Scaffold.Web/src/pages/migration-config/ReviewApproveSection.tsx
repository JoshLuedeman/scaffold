import { useState } from 'react';
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  DialogTrigger,
  Field,
  MessageBar,
  MessageBarBody,
  Text,
  Textarea,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import type { MigrationPlan, MigrationScript } from '../../types';
import { getStrategyInfo } from './types';
import type { CannedScriptInfo } from './types';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingHorizontalXL,
  },
  cardTitle: {
    fontWeight: tokens.fontWeightSemibold,
  },
  summaryRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    paddingTop: tokens.spacingVerticalXS,
    paddingBottom: tokens.spacingVerticalXS,
  },
  summaryLabel: {
    minWidth: '140px',
    fontWeight: tokens.fontWeightSemibold,
  },
  actionsRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    alignItems: 'center',
  },
  rejectionBanner: {
    marginTop: tokens.spacingVerticalM,
  },
});

interface ReviewApproveSectionProps {
  savedPlan: MigrationPlan;
  approving: boolean;
  rejecting: boolean;
  onApprove: () => void;
  onReject: (reason: string) => void;
  availableScripts: CannedScriptInfo[];
  selectedScripts: Set<string>;
  customScripts: MigrationScript[];
}

export function ReviewApproveSection({
  savedPlan,
  approving,
  rejecting,
  onApprove,
  onReject,
  availableScripts,
  selectedScripts,
  customScripts,
}: ReviewApproveSectionProps) {
  const styles = useStyles();
  const strategyInfo = getStrategyInfo(savedPlan.sourcePlatform);
  const [rejectDialogOpen, setRejectDialogOpen] = useState(false);
  const [rejectionReason, setRejectionReason] = useState('');

  function getStatusText(): string {
    if (savedPlan.isApproved) return `Approved by ${savedPlan.approvedBy}`;
    if (savedPlan.isRejected) return `Rejected by ${savedPlan.rejectedBy}`;
    return 'Pending approval';
  }

  function handleRejectConfirm() {
    if (!rejectionReason.trim()) return;
    onReject(rejectionReason.trim());
    setRejectDialogOpen(false);
    setRejectionReason('');
  }

  return (
    <Card className={styles.card}>
      <CardHeader header={<Text className={styles.cardTitle}>Review &amp; Approve</Text>} />

      {/* Rejection status banner */}
      {savedPlan.isRejected && (
        <MessageBar intent="error" className={styles.rejectionBanner}>
          <MessageBarBody>
            <Text weight="semibold">Plan rejected</Text>
            {savedPlan.rejectedBy && <Text> by {savedPlan.rejectedBy}</Text>}
            {savedPlan.rejectionReason && <Text>: {savedPlan.rejectionReason}</Text>}
          </MessageBarBody>
        </MessageBar>
      )}

      <div>
        <div className={styles.summaryRow}>
          <Text className={styles.summaryLabel}>Strategy</Text>
          <Text>{strategyInfo[savedPlan.strategy].label}</Text>
        </div>
        <div className={styles.summaryRow}>
          <Text className={styles.summaryLabel}>Objects</Text>
          <Text>{savedPlan.includedObjects.length} included, {savedPlan.excludedObjects.length} excluded</Text>
        </div>
        <div className={styles.summaryRow}>
          <Text className={styles.summaryLabel}>Service</Text>
          <Text>{savedPlan.targetTier.serviceTier}</Text>
        </div>
        <div className={styles.summaryRow}>
          <Text className={styles.summaryLabel}>Tier</Text>
          <Text>{savedPlan.targetTier.computeSize}</Text>
        </div>
        <div className={styles.summaryRow}>
          <Text className={styles.summaryLabel}>Schedule</Text>
          <Text>{savedPlan.scheduledAt ? new Date(savedPlan.scheduledAt).toLocaleString() : 'Immediately on approval'}</Text>
        </div>
        {(selectedScripts.size > 0 || customScripts.length > 0) && (
          <>
            <div className={styles.summaryRow}>
              <Text className={styles.summaryLabel}>Pre-migration</Text>
              <Text>
                {availableScripts.filter(s => s.phase === 'Pre' && selectedScripts.has(s.scriptId)).length} canned
                {customScripts.filter(s => s.phase === 'Pre').length > 0 && `, ${customScripts.filter(s => s.phase === 'Pre').length} custom`}
              </Text>
            </div>
            <div className={styles.summaryRow}>
              <Text className={styles.summaryLabel}>Post-migration</Text>
              <Text>
                {availableScripts.filter(s => s.phase === 'Post' && selectedScripts.has(s.scriptId)).length} canned
                {customScripts.filter(s => s.phase === 'Post').length > 0 && `, ${customScripts.filter(s => s.phase === 'Post').length} custom`}
              </Text>
            </div>
          </>
        )}
        <div className={styles.summaryRow}>
          <Text className={styles.summaryLabel}>Status</Text>
          <Text>
            {getStatusText()}
            {savedPlan.isRejected && (
              <>
                {' '}
                <Badge appearance="filled" color="danger">Rejected</Badge>
              </>
            )}
          </Text>
        </div>
      </div>
      <div className={styles.actionsRow} style={{ marginTop: tokens.spacingVerticalM }}>
        <Button
          appearance="primary"
          onClick={onApprove}
          disabled={approving || savedPlan.isApproved}
        >
          {approving ? 'Approving…' : savedPlan.isApproved ? 'Approved ✓' : 'Approve'}
        </Button>
        <Button
          appearance="secondary"
          onClick={() => setRejectDialogOpen(true)}
          disabled={rejecting || savedPlan.isApproved || savedPlan.isRejected}
        >
          {rejecting ? 'Rejecting…' : 'Reject Plan'}
        </Button>
      </div>

      {/* Rejection Dialog */}
      <Dialog open={rejectDialogOpen} onOpenChange={(_e, data) => setRejectDialogOpen(data.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Reject Migration Plan</DialogTitle>
            <DialogContent>
              <Field label="Rejection Reason" required>
                <Textarea
                  value={rejectionReason}
                  onChange={(_e, data) => setRejectionReason(data.value)}
                  placeholder="Explain why this plan is being rejected…"
                  resize="vertical"
                  style={{ minHeight: '100px' }}
                />
              </Field>
            </DialogContent>
            <DialogActions>
              <DialogTrigger disableButtonEnhancement>
                <Button appearance="secondary">Cancel</Button>
              </DialogTrigger>
              <Button
                appearance="primary"
                onClick={handleRejectConfirm}
                disabled={!rejectionReason.trim() || rejecting}
              >
                {rejecting ? 'Rejecting…' : 'Confirm Rejection'}
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </Card>
  );
}