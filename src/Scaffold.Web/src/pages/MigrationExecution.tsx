import { useState, useEffect, useRef } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useMsal } from '@azure/msal-react';
import type { PublicClientApplication } from '@azure/msal-browser';
import {
  Badge,
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbDivider,
  BreadcrumbButton,
  Button,
  Card,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogContent,
  DialogActions,
  DialogTrigger,
  MessageBar,
  MessageBarBody,
  ProgressBar,
  Table,
  TableHeader,
  TableHeaderCell,
  TableRow,
  TableCell,
  TableBody,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import {
  CheckmarkCircleRegular,
  DismissCircleRegular,
} from '@fluentui/react-icons';
import { api } from '../services/api';
import { useMigrationProgress } from '../hooks/useMigrationProgress';
import type { MigrationProgress } from '../hooks/useMigrationProgress';
import type { MigrationProject, MigrationResult, ValidationResult, DatabasePlatform } from '../types';

function useSafeMsal(): PublicClientApplication | null {
  const { instance, accounts } = useMsal();
  if (accounts.length === 0) {
    return null;
  }
  return instance as PublicClientApplication;
}

/** Map generic phase strings to PostgreSQL-friendly labels. */
const PG_PHASE_LABELS: Record<string, string> = {
  SchemaDeployment: 'Deploying schema',
  DataCopy: 'Copying data (COPY protocol)',
  LogicalReplication: 'Streaming WAL changes',
  InitialSync: 'Initial data sync',
  Validation: 'Validating data integrity',
  Cutover: 'Performing cutover',
};

function getPhaseLabel(phase: string, platform?: DatabasePlatform): string {
  if (platform === 'PostgreSql') {
    return PG_PHASE_LABELS[phase] ?? phase;
  }
  return phase;
}

function formatLagBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

const useStyles = makeStyles({
  root: {
    maxWidth: '800px',
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  card: {
    padding: tokens.spacingVerticalL,
  },
  connectionRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  statusRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    textTransform: 'capitalize' as const,
  },
  progressMeta: {
    display: 'flex',
    gap: tokens.spacingHorizontalXL,
    marginTop: tokens.spacingVerticalS,
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  replicationLag: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalXS,
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  logContainer: {
    maxHeight: '260px',
    overflowY: 'auto',
    backgroundColor: tokens.colorNeutralBackground6,
    borderRadius: tokens.borderRadiusMedium,
    padding: tokens.spacingVerticalS,
    fontFamily: 'Consolas, "Courier New", monospace',
    fontSize: tokens.fontSizeBase200,
  },
  logEntry: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    paddingTop: '2px',
    paddingBottom: '2px',
  },
  logTime: {
    color: tokens.colorNeutralForeground4,
    flexShrink: 0,
  },
  logMsg: {
    color: tokens.colorNeutralForeground1,
    wordBreak: 'break-word',
  },
  cancelButton: {
    color: tokens.colorPaletteRedForeground1,
  },
  cancelledActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalS,
  },
});

type MigrationStrategy = 'Cutover' | 'ContinuousSync';

const connectionBadgeColor: Record<string, 'success' | 'warning' | 'danger' | 'informative'> = {
  connected: 'success',
  connecting: 'warning',
  reconnecting: 'warning',
  disconnected: 'danger',
};

const statusBadgeColor: Record<string, 'success' | 'danger' | 'informative' | 'brand' | 'warning'> = {
  running: 'brand',
  completed: 'success',
  failed: 'danger',
  idle: 'informative',
  cancelled: 'warning',
};

export default function MigrationExecution() {
  const { id } = useParams<{ id: string }>();
  const msalInstance = useSafeMsal();

  const [project, setProject] = useState<MigrationProject | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [migrationId, setMigrationId] = useState<string | null>(null);
  const [starting, setStarting] = useState(false);
  const [cutoverPending, setCutoverPending] = useState(false);
  const [validating, setValidating] = useState(false);
  const [result, setResult] = useState<MigrationResult | null>(null);

  // Cancel state
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [cancelMessage, setCancelMessage] = useState<string | null>(null);

  // Recovery state
  const [recoveredStatus, setRecoveredStatus] = useState<'idle' | 'running' | 'completed' | 'failed' | 'cancelled'>('idle');
  const [recoveredProgress, setRecoveredProgress] = useState<MigrationProgress | null>(null);
  const recoveryDoneRef = useRef(false);

  const { progress, connectionStatus, log, migrationStatus } =
    useMigrationProgress(migrationId, msalInstance);

  // Effective status: prefer live hook status over recovered status
  const effectiveMigrationStatus = migrationStatus !== 'idle' ? migrationStatus : recoveredStatus;
  const effectiveProgress = progress ?? recoveredProgress;

  const logEndRef = useRef<HTMLDivElement>(null);
  const styles = useStyles();

  const strategy: MigrationStrategy | undefined = project?.migrationPlan?.strategy;
  const sourcePlatform: DatabasePlatform | undefined =
    project?.migrationPlan?.sourcePlatform ?? project?.sourceConnection?.platform;

  // Fetch project
  useEffect(() => {
    api
      .get<MigrationProject>(`/projects/${id}`)
      .then(setProject)
      .catch((err: unknown) => {
        setError(err instanceof Error ? err.message : 'Failed to load project');
      })
      .finally(() => setLoading(false));
  }, [id]);

  // State recovery: restore migration state from persisted plan on mount
  useEffect(() => {
    if (!project?.migrationPlan || recoveryDoneRef.current) return;
    recoveryDoneRef.current = true;
    const plan = project.migrationPlan;

    if (plan.status === 'Running' && plan.migrationId) {
      setRecoveredStatus('running');
      setMigrationId(plan.migrationId);
      // Back-fill progress from API while SignalR reconnects
      api.get<MigrationProgress>(`/projects/${id}/migrations/${plan.migrationId}/progress`)
        .then(setRecoveredProgress)
        .catch(() => {});
    } else if (plan.status === 'Completed' && plan.migrationId) {
      setRecoveredStatus('completed');
      setMigrationId(plan.migrationId);
    } else if (plan.status === 'Failed' && plan.migrationId) {
      setRecoveredStatus('failed');
      setMigrationId(plan.migrationId);
    } else if (plan.status === 'Cancelled') {
      setRecoveredStatus('cancelled');
    }
  }, [project, id]);

  // Auto-scroll log
  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [log]);

  // Fetch result on completion or failure
  useEffect(() => {
    if ((effectiveMigrationStatus === 'completed' || effectiveMigrationStatus === 'failed') && migrationId && !result) {
      api.get<MigrationResult>(`/projects/${id}/migrations/${migrationId}`).then(setResult).catch(() => {});
    }
  }, [effectiveMigrationStatus, migrationId, id, result]);

  async function startMigration() {
    setStarting(true);
    setError(null);
    setCancelMessage(null);
    try {
      const res = await api.post<{ migrationId: string }>(`/projects/${id}/migrations/start`, {});
      setMigrationId(res.migrationId);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to start migration');
    } finally {
      setStarting(false);
    }
  }

  async function cancelMigration() {
    if (!migrationId) return;
    setCancelling(true);
    setCancelMessage(null);
    setError(null);
    try {
      await api.post(`/projects/${id}/migrations/${migrationId}/cancel`, {});
      setCancelMessage('Migration was cancelled');
      setCancelDialogOpen(false);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to cancel migration');
      setCancelDialogOpen(false);
    } finally {
      setCancelling(false);
    }
  }

  function resetMigrationState() {
    setMigrationId(null);
    setRecoveredStatus('idle');
    setRecoveredProgress(null);
    setCancelMessage(null);
    setResult(null);
    setError(null);
    recoveryDoneRef.current = false;
  }

  async function triggerCutover() {
    if (!migrationId) return;
    setCutoverPending(true);
    try {
      await api.post(`/projects/${id}/migrations/${migrationId}/cutover`, {});
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Cutover failed');
    } finally {
      setCutoverPending(false);
    }
  }

  async function runValidation() {
    if (!migrationId) return;
    setValidating(true);
    try {
      const res = await api.post<MigrationResult>(
        `/projects/${id}/migrations/${migrationId}/validate`,
        {},
      );
      setResult(res);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Validation failed');
    } finally {
      setValidating(false);
    }
  }

  const cutoverMessage = sourcePlatform === 'PostgreSql'
    ? 'This will stop logical replication and finalize'
    : 'This will stop change tracking and finalize';

  if (loading) return <p>Loading…</p>;
  if (error && !migrationId) {
    return (
      <MessageBar intent="error">
        <MessageBarBody>{error}</MessageBarBody>
      </MessageBar>
    );
  }
  if (!project) return null;

  return (
    <div className={styles.root}>
      <Breadcrumb>
        <BreadcrumbItem>
          <BreadcrumbButton as={Link as never} {...({ to: '/' } as object)}>Projects</BreadcrumbButton>
        </BreadcrumbItem>
        <BreadcrumbDivider />
        <BreadcrumbItem>
          <BreadcrumbButton as={Link as never} {...({ to: `/projects/${id}` } as object)}>
            Project {id}
          </BreadcrumbButton>
        </BreadcrumbItem>
        <BreadcrumbDivider />
        <BreadcrumbItem>
          <BreadcrumbButton current>Execute Migration</BreadcrumbButton>
        </BreadcrumbItem>
      </Breadcrumb>

      <Text as="h2" size={600} weight="semibold">Execute Migration</Text>

      {/* Start — only when no active/recovered migration */}
      {!migrationId && effectiveMigrationStatus !== 'cancelled' && (
        <Card className={styles.card}>
          <Text block>
            Strategy: <Text weight="semibold">{strategy ?? 'N/A'}</Text>
          </Text>
          <Button appearance="primary" onClick={startMigration} disabled={starting}>
            {starting ? 'Starting…' : 'Start Migration'}
          </Button>
        </Card>
      )}

      {/* Cancelled state (recovered or live) */}
      {effectiveMigrationStatus === 'cancelled' && (
        <Card className={styles.card}>
          <MessageBar intent="warning">
            <MessageBarBody>
              Migration was cancelled. Any data already migrated remains in the target database.
            </MessageBarBody>
          </MessageBar>
          <div className={styles.cancelledActions}>
            <Button appearance="primary" onClick={resetMigrationState}>
              Start New Migration
            </Button>
            <Button
              appearance="secondary"
              as={Link as never}
              {...({ to: `/projects/${id}/configure` } as object)}
            >
              Reconfigure
            </Button>
          </div>
        </Card>
      )}

      {/* Connection indicator */}
      {migrationId && (
        <div className={styles.connectionRow}>
          <Badge
            appearance="filled"
            color={connectionBadgeColor[connectionStatus] ?? 'informative'}
            size="small"
          />
          <Text size={200}>{connectionStatus}</Text>
        </div>
      )}

      {/* Status indicator */}
      {migrationId && (
        <div className={styles.statusRow}>
          <Badge
            appearance="filled"
            color={statusBadgeColor[effectiveMigrationStatus] ?? 'informative'}
          >
            {effectiveMigrationStatus}
          </Badge>
        </div>
      )}

      {/* Progress */}
      {effectiveProgress && (
        <Card className={styles.card}>
          <Text weight="semibold" size={400}>Progress</Text>
          <Text weight="semibold" size={200}>{getPhaseLabel(effectiveProgress.phase, sourcePlatform)}</Text>
          <ProgressBar value={effectiveProgress.percentComplete / 100} />
          <div className={styles.progressMeta}>
            <span>{effectiveProgress.percentComplete}%</span>
            <span>Table: {effectiveProgress.currentTable || '—'}</span>
            <span>Rows: {effectiveProgress.rowsProcessed.toLocaleString()}</span>
          </div>
          {/* Replication lag indicator for ContinuousSync PG migrations */}
          {sourcePlatform === 'PostgreSql' && strategy === 'ContinuousSync' && effectiveProgress.replicationLagBytes != null && (
            <div className={styles.replicationLag}>
              <Text size={200} weight="semibold">Replication lag:</Text>
              <Badge
                appearance="filled"
                color={effectiveProgress.replicationLagBytes < 1024 * 1024 ? 'success' : effectiveProgress.replicationLagBytes < 10 * 1024 * 1024 ? 'warning' : 'danger'}
                size="small"
              >
                {formatLagBytes(effectiveProgress.replicationLagBytes)}
              </Badge>
            </div>
          )}
        </Card>
      )}

      {/* Cancel migration button */}
      {effectiveMigrationStatus === 'running' && migrationId && !cancelMessage && (
        <Button
          appearance="subtle"
          className={styles.cancelButton}
          onClick={() => setCancelDialogOpen(true)}
          disabled={cancelling}
        >
          {cancelling ? 'Cancelling…' : 'Cancel Migration'}
        </Button>
      )}

      {/* Cancel confirmation dialog */}
      <Dialog open={cancelDialogOpen} onOpenChange={(_, data) => setCancelDialogOpen(data.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Cancel Migration?</DialogTitle>
            <DialogContent>
              Are you sure? This will stop the migration. Any data already migrated will remain in the target database.
            </DialogContent>
            <DialogActions>
              <DialogTrigger disableButtonEnhancement>
                <Button appearance="secondary">Keep Running</Button>
              </DialogTrigger>
              <Button appearance="primary" onClick={cancelMigration} disabled={cancelling}>
                {cancelling ? 'Cancelling…' : 'Yes, Cancel Migration'}
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      {/* Cancel success message */}
      {cancelMessage && effectiveMigrationStatus !== 'cancelled' && (
        <MessageBar intent="warning">
          <MessageBarBody>{cancelMessage}</MessageBarBody>
        </MessageBar>
      )}

      {/* Cutover button for continuous sync */}
      {strategy === 'ContinuousSync' && effectiveMigrationStatus === 'running' && (
        <Card className={styles.card}>
          <Text size={200} style={{ color: tokens.colorNeutralForeground3, marginBottom: tokens.spacingVerticalS, display: 'block' }}>
            {cutoverMessage}
          </Text>
          <Button
            appearance="primary"
            onClick={triggerCutover}
            disabled={cutoverPending}
          >
            {cutoverPending ? 'Triggering Cutover…' : 'Trigger Cutover'}
          </Button>
        </Card>
      )}

      {/* Message log */}
      {migrationId && (
        <Card className={styles.card}>
          <Text weight="semibold" size={400}>Log</Text>
          <div className={styles.logContainer}>
            {log.map((entry, i) => (
              <div key={i} className={styles.logEntry}>
                <span className={styles.logTime}>
                  {entry.timestamp.toLocaleTimeString()}
                </span>
                <span className={styles.logMsg}>{entry.message}</span>
              </div>
            ))}
            <div ref={logEndRef} />
          </div>
        </Card>
      )}

      {/* Error display */}
      {error && migrationId && (
        <MessageBar intent="error">
          <MessageBarBody>{error}</MessageBarBody>
        </MessageBar>
      )}

      {/* Validation */}
      {effectiveMigrationStatus === 'completed' && !result && (
        <Card className={styles.card}>
          <Button appearance="primary" onClick={runValidation} disabled={validating}>
            {validating ? 'Validating…' : 'Run Validation'}
          </Button>
        </Card>
      )}

      {/* Validation results */}
      {result && (
        <Card className={styles.card}>
          <Text weight="semibold" size={400}>Validation Results</Text>
          <MessageBar intent={result.success ? 'success' : 'error'}>
            <MessageBarBody>
              {result.success ? 'All validations passed' : 'Some validations failed'}
            </MessageBarBody>
          </MessageBar>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHeaderCell>Table</TableHeaderCell>
                <TableHeaderCell>Source Rows</TableHeaderCell>
                <TableHeaderCell>Target Rows</TableHeaderCell>
                <TableHeaderCell>Checksum</TableHeaderCell>
                <TableHeaderCell>Result</TableHeaderCell>
              </TableRow>
            </TableHeader>
            <TableBody>
              {result.validations.map((v: ValidationResult) => (
                <TableRow key={v.tableName}>
                  <TableCell>{v.tableName}</TableCell>
                  <TableCell>{v.sourceRowCount.toLocaleString()}</TableCell>
                  <TableCell>{v.targetRowCount.toLocaleString()}</TableCell>
                  <TableCell>
                    {v.checksumMatch
                      ? <CheckmarkCircleRegular style={{ color: tokens.colorPaletteGreenForeground1 }} />
                      : <DismissCircleRegular style={{ color: tokens.colorPaletteRedForeground1 }} />}
                  </TableCell>
                  <TableCell>
                    <Badge
                      appearance="filled"
                      color={v.passed ? 'success' : 'danger'}
                    >
                      {v.passed ? 'Pass' : 'Fail'}
                    </Badge>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Card>
      )}
    </div>
  );
}