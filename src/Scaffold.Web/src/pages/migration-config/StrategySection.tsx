import {
  Badge,
  Card,
  CardHeader,
  MessageBar,
  MessageBarBody,
  RadioGroup,
  Radio,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import type { MigrationStrategy, DatabasePlatform, StrategyRecommendation } from '../../types';
import { getStrategyInfo } from './types';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingHorizontalXL,
  },
  cardTitle: {
    fontWeight: tokens.fontWeightSemibold,
  },
  radioDescription: {
    display: 'block',
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase200,
  },
  recommendationBanner: {
    marginBottom: tokens.spacingVerticalM,
  },
  radioLabel: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
  },
  downtimeHint: {
    display: 'block',
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase200,
    fontStyle: 'italic',
  },
});

interface StrategySectionProps {
  strategy: MigrationStrategy;
  onStrategyChange: (strategy: MigrationStrategy) => void;
  sourcePlatform?: DatabasePlatform;
  strategyRecommendation?: StrategyRecommendation;
}

export function StrategySection({ strategy, onStrategyChange, sourcePlatform, strategyRecommendation }: StrategySectionProps) {
  const styles = useStyles();
  const strategyInfo = getStrategyInfo(sourcePlatform);

  return (
    <Card className={styles.card}>
      <CardHeader header={<Text className={styles.cardTitle}>Migration Strategy</Text>} />
      {strategyRecommendation && (
        <MessageBar intent="info" className={styles.recommendationBanner}>
          <MessageBarBody>
            Based on your assessment, we recommend{' '}
            <Text weight="semibold">
              {strategyRecommendation.recommendedStrategy === 'ContinuousSync' ? 'Continuous Sync' : 'Cutover'}
            </Text>
          </MessageBarBody>
        </MessageBar>
      )}
      <RadioGroup
        value={strategy}
        onChange={(_e, data) => onStrategyChange(data.value as MigrationStrategy)}
      >
        {(Object.keys(strategyInfo) as MigrationStrategy[]).map((key) => (
          <Radio
            key={key}
            value={key}
            label={
              <>
                <span className={styles.radioLabel}>
                  {strategyInfo[key].label}
                  {strategyRecommendation?.recommendedStrategy === key && (
                    <Badge appearance="filled" color="brand" size="small">Recommended</Badge>
                  )}
                </span>
                <span className={styles.radioDescription}>{strategyInfo[key].description}</span>
                {strategyRecommendation && (
                  <span className={styles.downtimeHint}>
                    Est. downtime: {key === 'Cutover'
                      ? strategyRecommendation.estimatedDowntimeCutover
                      : strategyRecommendation.estimatedDowntimeContinuousSync ?? 'N/A'}
                  </span>
                )}
              </>
            }
          />
        ))}
      </RadioGroup>
    </Card>
  );
}