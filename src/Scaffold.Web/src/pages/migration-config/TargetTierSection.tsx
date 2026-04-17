import {
  Card,
  CardHeader,
  Field,
  MessageBar,
  MessageBarBody,
  Select,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import type { DatabasePlatform, RegionPricing, TierRecommendation } from '../../types';
import { getServicesForPlatform } from './types';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingHorizontalXL,
  },
  cardTitle: {
    fontWeight: tokens.fontWeightSemibold,
  },
});

interface TargetTierSectionProps {
  recommendation?: TierRecommendation;
  regionPricing: RegionPricing[];
  serviceOverride: string;
  tierOverride: string;
  onServiceOverrideChange: (value: string) => void;
  onTierOverrideChange: (value: string) => void;
  sourcePlatform?: DatabasePlatform;
}

export function TargetTierSection({
  recommendation,
  regionPricing,
  serviceOverride,
  tierOverride,
  onServiceOverrideChange,
  onTierOverrideChange,
  sourcePlatform,
}: TargetTierSectionProps) {
  const styles = useStyles();
  const services = getServicesForPlatform(sourcePlatform);

  return (
    <Card className={styles.card}>
      <CardHeader header={<Text className={styles.cardTitle}>Target Tier</Text>} />
      {recommendation && (
        <MessageBar intent="info" style={{ marginBottom: tokens.spacingVerticalM }}>
          <MessageBarBody>
            Recommended: <strong>{recommendation.serviceTier}</strong> — {recommendation.computeSize}{' '}
            (~${regionPricing.length > 0 ? regionPricing[0].estimatedMonthlyCostUsd.toFixed(2) : recommendation.estimatedMonthlyCostUsd}/mo)
          </MessageBarBody>
        </MessageBar>
      )}
      <Field label="Target Service">
        <Select
          value={serviceOverride}
          onChange={(_e, data) => onServiceOverrideChange(data.value)}
        >
          <option value="">Use recommended</option>
          {services.map((s) => (
            <option key={s.service} value={s.service}>{s.service}</option>
          ))}
        </Select>
      </Field>
      {serviceOverride && (
        <Field label="Service Tier" style={{ marginTop: tokens.spacingVerticalS }}>
          <Select
            value={tierOverride}
            onChange={(_e, data) => onTierOverrideChange(data.value)}
          >
            <option value="">Use recommended</option>
            {services.find((s) => s.service === serviceOverride)?.tiers.map((t) => (
              <option key={t} value={t}>{t}</option>
            ))}
          </Select>
        </Field>
      )}
    </Card>
  );
}