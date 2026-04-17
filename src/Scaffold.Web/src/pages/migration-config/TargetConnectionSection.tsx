import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  Field,
  Input,
  MessageBar,
  MessageBarBody,
  Select,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import type { DatabasePlatform } from '../../types';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingHorizontalXL,
  },
  cardTitle: {
    fontWeight: tokens.fontWeightSemibold,
  },
  headerRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  form: {
    display: 'flex',
    flexDirection: 'column',
    gap: '12px',
    marginTop: '12px',
  },
  fieldRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
  },
  portField: {
    maxWidth: '120px',
  },
  testRow: {
    display: 'flex',
    gap: '8px',
    alignItems: 'center',
  },
});

export interface TargetConnectionSectionProps {
  useExistingTarget: boolean;
  onUseExistingTargetChange: (value: boolean) => void;
  targetServer: string;
  onTargetServerChange: (value: string) => void;
  targetDatabase: string;
  onTargetDatabaseChange: (value: string) => void;
  targetUsername: string;
  onTargetUsernameChange: (value: string) => void;
  targetPassword: string;
  onTargetPasswordChange: (value: string) => void;
  targetPort: string;
  onTargetPortChange: (value: string) => void;
  targetSslMode: string;
  onTargetSslModeChange: (value: string) => void;
  targetAuthType: string;
  onTargetAuthTypeChange: (value: string) => void;
  testingTarget: boolean;
  targetTestResult: { ok: boolean; message: string } | null;
  onTestConnection: () => void;
  sourcePlatform?: DatabasePlatform;
  targetPlatform?: DatabasePlatform;
}

export function TargetConnectionSection({
  useExistingTarget,
  onUseExistingTargetChange,
  targetServer,
  onTargetServerChange,
  targetDatabase,
  onTargetDatabaseChange,
  targetUsername,
  onTargetUsernameChange,
  targetPassword,
  onTargetPasswordChange,
  targetPort,
  onTargetPortChange,
  targetSslMode,
  onTargetSslModeChange,
  targetAuthType,
  onTargetAuthTypeChange,
  testingTarget,
  targetTestResult,
  onTestConnection,
  sourcePlatform,
  targetPlatform,
}: TargetConnectionSectionProps) {
  const styles = useStyles();
  const platform = targetPlatform ?? sourcePlatform;
  const isPostgres = platform === 'PostgreSql';

  const portNum = parseInt(targetPort, 10);
  const portValid =
    targetPort === '' || (/^\d+$/.test(targetPort) && portNum > 0 && portNum <= 65535);

  const hasRequiredFields = isPostgres
    ? !!(targetServer && targetDatabase && targetUsername && targetPassword && targetPort && portValid)
    : !!(
        targetServer &&
        targetDatabase &&
        (targetAuthType === 'Windows' || (targetUsername && targetPassword))
      );

  return (
    <Card className={styles.card}>
      <CardHeader
        header={
          <div className={styles.headerRow}>
            <Text className={styles.cardTitle}>Target Database</Text>
            {platform && (
              <Badge
                appearance="filled"
                color={isPostgres ? 'brand' : 'informative'}
              >
                {isPostgres ? 'PostgreSQL' : 'SQL Server'}
              </Badge>
            )}
          </div>
        }
      />
      <Checkbox
        label={
          isPostgres
            ? 'Use an existing Azure Database for PostgreSQL as the migration target'
            : 'Use an existing Azure SQL database as the migration target'
        }
        checked={useExistingTarget}
        onChange={(_, data) => onUseExistingTargetChange(data.checked === true)}
      />
      {useExistingTarget && (
        <div className={styles.form}>
          {isPostgres ? (
            <>
              <Field label="Host" required>
                <Input
                  placeholder="myserver.postgres.database.azure.com"
                  value={targetServer}
                  onChange={(_, d) => onTargetServerChange(d.value)}
                />
              </Field>
              <div className={styles.fieldRow}>
                <Field
                  label="Port"
                  required
                  className={styles.portField}
                  validationMessage={
                    !portValid
                      ? 'Must be a valid port number (1\u201365535)'
                      : undefined
                  }
                  validationState={!portValid ? 'error' : 'none'}
                >
                  <Input
                    value={targetPort}
                    onChange={(_, d) => onTargetPortChange(d.value)}
                  />
                </Field>
                <Field label="Database" required style={{ flex: 1 }}>
                  <Input
                    value={targetDatabase}
                    onChange={(_, d) => onTargetDatabaseChange(d.value)}
                  />
                </Field>
              </div>
              <Field label="Username" required>
                <Input
                  value={targetUsername}
                  onChange={(_, d) => onTargetUsernameChange(d.value)}
                />
              </Field>
              <Field label="Password" required>
                <Input
                  type="password"
                  value={targetPassword}
                  onChange={(_, d) => onTargetPasswordChange(d.value)}
                />
              </Field>
              <Field label="SSL Mode">
                <Select
                  value={targetSslMode}
                  onChange={(_, d) => onTargetSslModeChange(d.value)}
                >
                  <option value="Require">Require</option>
                  <option value="Prefer">Prefer</option>
                  <option value="Disable">Disable</option>
                </Select>
              </Field>
            </>
          ) : (
            <>
              <Field label="Server" required>
                <Input
                  placeholder="myserver.database.windows.net"
                  value={targetServer}
                  onChange={(_, d) => onTargetServerChange(d.value)}
                />
              </Field>
              <Field label="Database" required>
                <Input
                  value={targetDatabase}
                  onChange={(_, d) => onTargetDatabaseChange(d.value)}
                />
              </Field>
              <Field label="Authentication">
                <Select
                  value={targetAuthType}
                  onChange={(_, d) => onTargetAuthTypeChange(d.value)}
                >
                  <option value="SQL">SQL Authentication</option>
                  <option value="Windows">Windows Authentication</option>
                </Select>
              </Field>
              {targetAuthType === 'SQL' && (
                <>
                  <Field label="Username" required>
                    <Input
                      value={targetUsername}
                      onChange={(_, d) => onTargetUsernameChange(d.value)}
                    />
                  </Field>
                  <Field label="Password" required>
                    <Input
                      type="password"
                      value={targetPassword}
                      onChange={(_, d) => onTargetPasswordChange(d.value)}
                    />
                  </Field>
                </>
              )}
            </>
          )}
          <div className={styles.testRow}>
            <Button
              appearance="secondary"
              onClick={onTestConnection}
              disabled={testingTarget || !hasRequiredFields}
            >
              {testingTarget ? 'Testing\u2026' : 'Test Connection'}
            </Button>
            {targetTestResult && (
              <MessageBar
                intent={targetTestResult.ok ? 'success' : 'error'}
              >
                <MessageBarBody>
                  {targetTestResult.message}
                </MessageBarBody>
              </MessageBar>
            )}
          </div>
        </div>
      )}
    </Card>
  );
}
