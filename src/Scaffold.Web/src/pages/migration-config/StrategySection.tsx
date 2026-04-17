import {
  Card,
  CardHeader,
  RadioGroup,
  Radio,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import type { MigrationStrategy, DatabasePlatform } from '../../types';
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
});

interface StrategySectionProps {
  strategy: MigrationStrategy;
  onStrategyChange: (strategy: MigrationStrategy) => void;
  sourcePlatform?: DatabasePlatform;
}

export function StrategySection({ strategy, onStrategyChange, sourcePlatform }: StrategySectionProps) {
  const styles = useStyles();
  const strategyInfo = getStrategyInfo(sourcePlatform);

  return (
    <Card className={styles.card}>
      <CardHeader header={<Text className={styles.cardTitle}>Migration Strategy</Text>} />
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
                {strategyInfo[key].label}
                <span className={styles.radioDescription}>{strategyInfo[key].description}</span>
              </>
            }
          />
        ))}
      </RadioGroup>
    </Card>
  );
}