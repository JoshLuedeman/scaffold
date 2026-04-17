import {
  Card,
  CardHeader,
  MessageBar,
  MessageBarBody,
  Radio,
  RadioGroup,
  Spinner,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import type { RegionPricing } from '../../types';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingHorizontalXL,
  },
  cardTitle: {
    fontWeight: tokens.fontWeightSemibold,
  },
});

interface RegionPricingSectionProps {
  regionPricing: RegionPricing[];
  selectedRegion: string;
  onRegionChange: (value: string) => void;
  loadingPricing: boolean;
}

export function RegionPricingSection({
  regionPricing,
  selectedRegion,
  onRegionChange,
  loadingPricing,
}: RegionPricingSectionProps) {
  const styles = useStyles();

  return (
    <Card className={styles.card}>
      <CardHeader header={<Text className={styles.cardTitle}>Deployment Region</Text>} />
      {loadingPricing && <Spinner label="Loading pricing…" />}
      {!loadingPricing && regionPricing.length === 0 && (
        <MessageBar intent="info">
          <MessageBarBody>No pricing data available</MessageBarBody>
        </MessageBar>
      )}
      {!loadingPricing && regionPricing.length > 0 && (
        <>
          <RadioGroup
            value={selectedRegion}
            onChange={(_e, data) => onRegionChange(data.value)}
          >
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr style={{ textAlign: 'left', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>
                  <th style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}` }}></th>
                  <th style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}` }}>Region</th>
                  <th style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}` }}>Region Code</th>
                  <th style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`, textAlign: 'right' }}>Est. Monthly Cost</th>
                </tr>
              </thead>
              <tbody>
                {regionPricing.slice(0, 15).map((r) => (
                  <tr key={r.armRegionName} style={{ borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>
                    <td style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}` }}>
                      <Radio value={r.armRegionName} label="" />
                    </td>
                    <td style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}` }}>{r.displayName}</td>
                    <td style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}` }}>
                      <Text size={200} style={{ fontFamily: 'monospace' }}>{r.armRegionName}</Text>
                    </td>
                    <td style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`, textAlign: 'right' }}>
                      ${r.estimatedMonthlyCostUsd.toFixed(2)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </RadioGroup>
          {selectedRegion && (() => {
            const sel = regionPricing.find((r) => r.armRegionName === selectedRegion);
            return sel ? (
              <Text size={200} style={{ marginTop: tokens.spacingVerticalS, display: 'block' }}>
                Selected: {sel.displayName} — ~${sel.estimatedMonthlyCostUsd.toFixed(2)}/mo
              </Text>
            ) : null;
          })()}
        </>
      )}
    </Card>
  );
}