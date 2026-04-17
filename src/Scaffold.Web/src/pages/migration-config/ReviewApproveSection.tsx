import {
  Button,
  Card,
  CardHeader,
  Text,
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
});

interface ReviewApproveSectionProps {
  savedPlan: MigrationPlan;
  approving: boolean;
  onApprove: () => void;
  availableScripts: CannedScriptInfo[];
  selectedScripts: Set<string>;
  customScripts: MigrationScript[];
}

export function ReviewApproveSection({
  savedPlan,
  approving,
  onApprove,
  availableScripts,
  selectedScripts,
  customScripts,
}: ReviewApproveSectionProps) {
  const styles = useStyles();
  const strategyInfo = getStrategyInfo(savedPlan.sourcePlatform);

  return (
    <Card className={styles.card}>
      <CardHeader header={<Text className={styles.cardTitle}>Review &amp; Approve</Text>} />
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
          <Text>{savedPlan.isApproved ? `Approved by ${savedPlan.approvedBy}` : 'Pending approval'}</Text>
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
      </div>
    </Card>
  );
}