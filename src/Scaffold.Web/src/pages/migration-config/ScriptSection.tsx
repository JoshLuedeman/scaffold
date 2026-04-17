import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { AddRegular, EyeRegular } from '@fluentui/react-icons';
import type { MigrationScript } from '../../types';
import type { CannedScriptInfo } from './types';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingHorizontalXL,
  },
  cardTitle: {
    fontWeight: tokens.fontWeightSemibold,
  },
});

interface ScriptSectionProps {
  phase: 'Pre' | 'Post';
  availableScripts: CannedScriptInfo[];
  selectedScripts: Set<string>;
  customScripts: MigrationScript[];
  onToggleScript: (scriptId: string) => void;
  onPreviewScript: (scriptId: string) => void;
  onRemoveCustomScript: (scriptId: string) => void;
  onAddCustomScript: (phase: 'Pre' | 'Post') => void;
}

export function ScriptSection({
  phase,
  availableScripts,
  selectedScripts,
  customScripts,
  onToggleScript,
  onPreviewScript,
  onRemoveCustomScript,
  onAddCustomScript,
}: ScriptSectionProps) {
  const styles = useStyles();
  const phaseScripts = availableScripts.filter(s => s.phase === phase);
  const phaseCustomScripts = customScripts.filter(s => s.phase === phase);

  return (
    <Card className={styles.card}>
      <CardHeader header={<Text className={styles.cardTitle}>{phase}-Migration Scripts</Text>} />
      {phaseScripts.map(script => (
        <div key={script.scriptId} style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS, marginBottom: tokens.spacingVerticalXS }}>
          <Checkbox
            checked={selectedScripts.has(script.scriptId)}
            onChange={() => onToggleScript(script.scriptId)}
            label={`${script.label} (${script.objectCount})`}
          />
          <Button
            appearance="subtle"
            size="small"
            icon={<EyeRegular />}
            onClick={() => onPreviewScript(script.scriptId)}
            title="Preview SQL"
          />
        </div>
      ))}
      {phaseCustomScripts.map(script => (
        <div key={script.scriptId} style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS, marginBottom: tokens.spacingVerticalXS }}>
          <Badge appearance="outline">Custom</Badge>
          <Text>{script.label}</Text>
          <Button appearance="subtle" size="small" onClick={() => onRemoveCustomScript(script.scriptId)}>✕</Button>
        </div>
      ))}
      <Button
        appearance="outline"
        size="small"
        icon={<AddRegular />}
        onClick={() => onAddCustomScript(phase)}
        style={{ marginTop: tokens.spacingVerticalS }}
      >
        Add Custom Script
      </Button>
    </Card>
  );
}