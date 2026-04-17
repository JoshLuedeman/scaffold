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
import type { RegionPricing, TierRecommendation } from '../../types';
import { getCostBarColor } from './types';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingHorizontalXL,
  },
  cardTitle: {
    fontWeight: tokens.fontWeightSemibold,
  },
  costBar: {
    height: '16px',
    borderRadius: tokens.borderRadiusSmall,
    transitionProperty: 'width',
    transitionDuration: '0.3s',
    minWidth: '4px',
  },
  comparisonTable: {
    width: '100%',
    borderCollapse: 'collapse',
    marginTop: tokens.spacingVerticalM,
    marginBottom: tokens.spacingVerticalM,
  },
  summaryCard: {
    marginTop: tokens.spacingVerticalM,
    paddingTop: tokens.spacingVerticalM,
    paddingBottom: tokens.spacingVerticalM,
    paddingLeft: tokens.spacingHorizontalL,
    paddingRight: tokens.spacingHorizontalL,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
  },
  summaryRow: {
    display: 'flex',
    justifyContent: 'space-between',
    paddingTop: tokens.spacingVerticalXS,
    paddingBottom: tokens.spacingVerticalXS,
  },
  summaryLabel: {
    fontWeight: tokens.fontWeightSemibold,
  },
  summaryAmount: {
    fontWeight: tokens.fontWeightBold,
    fontSize: tokens.fontSizeBase400,
  },
  annualAmount: {
    color: tokens.colorNeutralForeground3,
  },
});

function getCostBarColor(cost: number, minCost: number, maxCost: number): string {
  if (maxCost === minCost) return '#4caf50';
  const ratio = (cost - minCost) / (maxCost - minCost);
  // Green (#4caf50) -> Amber (#ff9800)
  const r = Math.round(76 + ratio * (255 - 76));
  const g = Math.round(175 + ratio * (152 - 175));
  const b = Math.round(80 + ratio * (0 - 80));
  return `rgb(${r}, ${g}, ${b})`;
}

interface RegionPricingSectionProps {
  regionPricing: RegionPricing[];
  selectedRegion: string;
  onRegionChange: (value: string) => void;
  loadingPricing: boolean;
  recommendation?: TierRecommendation;
  serviceOverride?: string;
  tierOverride?: string;
}

export function RegionPricingSection({
  regionPricing,
  selectedRegion,
  onRegionChange,
  loadingPricing,
  recommendation,
  serviceOverride,
  tierOverride,
}: RegionPricingSectionProps) {
  const styles = useStyles();

  const maxCost =
    regionPricing.length > 0
      ? Math.max(...regionPricing.map(r => r.estimatedMonthlyCostUsd))
      : 0;
  const minCost =
    regionPricing.length > 0
      ? Math.min(...regionPricing.map(r => r.estimatedMonthlyCostUsd))
      : 0;

  const selectedPricing = regionPricing.find(r => r.armRegionName === selectedRegion);
  const monthlyCost = selectedPricing?.estimatedMonthlyCostUsd ?? 0;
  const annualCost = monthlyCost * 12;

  const hasOverride = !!(
    serviceOverride &&
    recommendation &&
    serviceOverride !== recommendation.serviceTier
  );

  return (
    <Card className={styles.card}>
      <CardHeader
        header={<Text className={styles.cardTitle}>Deployment Region & Pricing</Text>}
      />
      {loadingPricing && <Spinner label="Loading pricing\u2026" />}
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
                <tr
                  style={{
                    textAlign: 'left',
                    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
                  }}
                >
                  <th style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}` }}></th>
                  <th style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}` }}>
                    Region
                  </th>
                  <th style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}` }}>
                    Region Code
                  </th>
                  <th
                    style={{
                      padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                      textAlign: 'right',
                    }}
                  >
                    Est. Monthly Cost
                  </th>
                  <th
                    style={{
                      padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                      width: '120px',
                    }}
                  >
                    Cost
                  </th>
                </tr>
              </thead>
              <tbody>
                {regionPricing.slice(0, 15).map(r => {
                  const barWidth =
                    maxCost > 0
                      ? (r.estimatedMonthlyCostUsd / maxCost) * 100
                      : 0;
                  const barColor = getCostBarColor(
                    r.estimatedMonthlyCostUsd,
                    minCost,
                    maxCost,
                  );
                  return (
                    <tr
                      key={r.armRegionName}
                      style={{
                        borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
                      }}
                    >
                      <td
                        style={{
                          padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                        }}
                      >
                        <Radio value={r.armRegionName} label="" />
                      </td>
                      <td
                        style={{
                          padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                        }}
                      >
                        {r.displayName}
                      </td>
                      <td
                        style={{
                          padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                        }}
                      >
                        <Text size={200} style={{ fontFamily: 'monospace' }}>
                          {r.armRegionName}
                        </Text>
                      </td>
                      <td
                        style={{
                          padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                          textAlign: 'right',
                        }}
                      >
                        ${r.estimatedMonthlyCostUsd.toFixed(2)}
                      </td>
                      <td
                        style={{
                          padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                        }}
                      >
                        <div
                          className={styles.costBar}
                          data-testid={`cost-bar-${r.armRegionName}`}
                          style={{
                            width: `${barWidth}%`,
                            backgroundColor: barColor,
                          }}
                        />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </RadioGroup>

          {/* Tier Comparison Table */}
          {hasOverride && recommendation && (
            <div data-testid="tier-comparison">
              <Text
                weight="semibold"
                size={300}
                style={{
                  display: 'block',
                  marginTop: tokens.spacingVerticalM,
                }}
              >
                Tier Comparison
              </Text>
              <table className={styles.comparisonTable}>
                <thead>
                  <tr
                    style={{
                      borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
                    }}
                  >
                    <th
                      style={{
                        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                        textAlign: 'left',
                      }}
                    ></th>
                    <th
                      style={{
                        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                        textAlign: 'left',
                      }}
                    >
                      Recommended
                    </th>
                    <th
                      style={{
                        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                        textAlign: 'left',
                      }}
                    >
                      Selected
                    </th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    style={{
                      borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
                    }}
                  >
                    <td
                      style={{
                        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                        fontWeight: 600,
                      }}
                    >
                      Service
                    </td>
                    <td
                      style={{
                        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                      }}
                    >
                      {recommendation.serviceTier}
                    </td>
                    <td
                      style={{
                        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                      }}
                    >
                      {serviceOverride}
                    </td>
                  </tr>
                  <tr
                    style={{
                      borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
                    }}
                  >
                    <td
                      style={{
                        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                        fontWeight: 600,
                      }}
                    >
                      Tier
                    </td>
                    <td
                      style={{
                        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                      }}
                    >
                      {recommendation.computeSize}
                    </td>
                    <td
                      style={{
                        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                      }}
                    >
                      {tierOverride || recommendation.computeSize}
                    </td>
                  </tr>
                  <tr
                    style={{
                      borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
                    }}
                  >
                    <td
                      style={{
                        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                        fontWeight: 600,
                      }}
                    >
                      Est. Cost
                    </td>
                    <td
                      style={{
                        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                      }}
                    >
                      ${recommendation.estimatedMonthlyCostUsd.toFixed(2)}/mo
                    </td>
                    <td
                      style={{
                        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
                      }}
                    >
                      {selectedPricing
                        ? `$${selectedPricing.estimatedMonthlyCostUsd.toFixed(2)}/mo`
                        : '\u2014'}
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          )}

          {/* Total Cost Summary Card */}
          {selectedRegion && selectedPricing && (
            <div className={styles.summaryCard} data-testid="cost-summary">
              <Text
                weight="semibold"
                size={400}
                style={{
                  display: 'block',
                  marginBottom: tokens.spacingVerticalS,
                }}
              >
                Cost Summary
              </Text>
              <div className={styles.summaryRow}>
                <Text className={styles.summaryLabel}>Region</Text>
                <Text>{selectedPricing.displayName}</Text>
              </div>
              <div className={styles.summaryRow}>
                <Text className={styles.summaryLabel}>Monthly Estimate</Text>
                <Text className={styles.summaryAmount}>
                  ${monthlyCost.toFixed(2)}
                </Text>
              </div>
              <div className={styles.summaryRow}>
                <Text className={styles.summaryLabel}>Annual Estimate</Text>
                <Text className={styles.annualAmount}>
                  ${annualCost.toFixed(2)}/yr
                </Text>
              </div>
            </div>
          )}
        </>
      )}
    </Card>
  );
}