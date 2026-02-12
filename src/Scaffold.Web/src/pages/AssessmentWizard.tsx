import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  Button,
  Card,
  Field,
  Input,
  Switch,
  Spinner,
  Text,
  MessageBar,
  MessageBarBody,
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbButton,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { api } from '../services/api';
import type { AssessmentReport as Report } from '../types';
import AssessmentReport from '../components/AssessmentReport';

type Step = 'connect' | 'assess' | 'review';

const STEPS: { key: Step; label: string }[] = [
  { key: 'connect', label: 'Connect' },
  { key: 'assess', label: 'Assess' },
  { key: 'review', label: 'Review' },
];

interface ConnectionForm {
  server: string;
  database: string;
  port: string;
  useSqlAuth: boolean;
  username: string;
  password: string;
}

const initialForm: ConnectionForm = {
  server: '',
  database: '',
  port: '1433',
  useSqlAuth: false,
  username: '',
  password: '',
};

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  stepper: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  stepItem: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  stepCircle: {
    display: 'inline-flex',
    alignItems: 'center',
    justifyContent: 'center',
    width: '28px',
    height: '28px',
    borderRadius: '50%',
    fontSize: tokens.fontSizeBase200,
    fontWeight: tokens.fontWeightSemibold,
  },
  stepCircleFuture: {
    backgroundColor: tokens.colorNeutralBackground3,
    color: tokens.colorNeutralForeground3,
  },
  stepCircleActive: {
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
  },
  stepCircleCompleted: {
    backgroundColor: tokens.colorPaletteGreenBackground3,
    color: tokens.colorNeutralForegroundOnBrand,
  },
  stepLabelFuture: {
    color: tokens.colorNeutralForeground3,
  },
  stepLabelActive: {
    color: tokens.colorNeutralForeground1,
    fontWeight: tokens.fontWeightSemibold,
  },
  stepLabelCompleted: {
    color: tokens.colorPaletteGreenForeground1,
  },
  stepDivider: {
    width: '32px',
    height: '2px',
    backgroundColor: tokens.colorNeutralBackground3,
  },
  formGrid: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: tokens.spacingVerticalM,
    maxWidth: '600px',
  },
  fullWidth: {
    gridColumn: '1 / -1',
  },
  actions: {
    gridColumn: '1 / -1',
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    alignItems: 'center',
    marginTop: tokens.spacingVerticalS,
  },
  assessCenter: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: tokens.spacingVerticalM,
    paddingTop: tokens.spacingVerticalXXL,
    paddingBottom: tokens.spacingVerticalXXL,
  },
  reviewActions: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalL,
  },
  breadcrumbLink: {
    textDecoration: 'none',
    color: 'inherit',
  },
});

export default function AssessmentWizard() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [step, setStep] = useState<Step>('connect');
  const [form, setForm] = useState<ConnectionForm>(initialForm);
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ ok: boolean; message: string } | null>(null);
  const [running, setRunning] = useState(false);
  const [report, setReport] = useState<Report | null>(null);
  const [error, setError] = useState<string | null>(null);
  const styles = useStyles();

  const stepIndex = STEPS.findIndex((s) => s.key === step);

  async function testConnection() {
    setTesting(true);
    setTestResult(null);
    try {
      await api.post('/connections/test', {
        server: form.server,
        database: form.database,
        port: parseInt(form.port, 10),
        useSqlAuthentication: form.useSqlAuth,
        username: form.useSqlAuth ? form.username : undefined,
        password: form.useSqlAuth ? form.password : undefined,
      });
      setTestResult({ ok: true, message: 'Connection successful' });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Connection failed';
      setTestResult({ ok: false, message });
    } finally {
      setTesting(false);
    }
  }

  async function runAssessment() {
    setRunning(true);
    setError(null);
    try {
      const result = await api.post<Report>(`/projects/${id}/assessments`, {
        server: form.server,
        database: form.database,
        port: parseInt(form.port, 10),
        useSqlAuthentication: form.useSqlAuth,
        username: form.useSqlAuth ? form.username : undefined,
        password: form.useSqlAuth ? form.password : undefined,
        trustServerCertificate: true,
      });
      setReport(result);
      setStep('review');
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Assessment failed';
      setError(message);
    } finally {
      setRunning(false);
    }
  }

  const canProceedToAssess = form.server && form.database && testResult?.ok;

  function stepCircleClass(i: number) {
    if (i < stepIndex) return `${styles.stepCircle} ${styles.stepCircleCompleted}`;
    if (i === stepIndex) return `${styles.stepCircle} ${styles.stepCircleActive}`;
    return `${styles.stepCircle} ${styles.stepCircleFuture}`;
  }

  function stepLabelClass(i: number) {
    if (i < stepIndex) return styles.stepLabelCompleted;
    if (i === stepIndex) return styles.stepLabelActive;
    return styles.stepLabelFuture;
  }

  return (
    <div className={styles.root}>
      <Breadcrumb>
        <BreadcrumbItem>
          <BreadcrumbButton as="a" href="/" onClick={(e) => { e.preventDefault(); navigate('/'); }}>
            Projects
          </BreadcrumbButton>
        </BreadcrumbItem>
        <BreadcrumbItem>
          <BreadcrumbButton as="a" href={`/projects/${id}`} onClick={(e) => { e.preventDefault(); navigate(`/projects/${id}`); }}>
            Project {id}
          </BreadcrumbButton>
        </BreadcrumbItem>
        <BreadcrumbItem>
          <BreadcrumbButton current>Assessment</BreadcrumbButton>
        </BreadcrumbItem>
      </Breadcrumb>

      <div className={styles.header}>
        <Text as="h2" size={700} weight="semibold">Assessment Wizard</Text>
      </div>

      <div className={styles.stepper}>
        {STEPS.map((s, i) => (
          <div key={s.key} className={styles.stepItem}>
            {i > 0 && <span className={styles.stepDivider} />}
            <span className={stepCircleClass(i)}>{i + 1}</span>
            <Text className={stepLabelClass(i)}>{s.label}</Text>
          </div>
        ))}
      </div>

      <Card>
        {step === 'connect' && (
          <>
            <Text as="h3" size={500} weight="semibold">Source Database Connection</Text>
            <div className={styles.formGrid}>
              <Field label="Server">
                <Input value={form.server} onChange={(_, d) => { setForm((prev) => ({ ...prev, server: d.value })); setTestResult(null); }} placeholder="e.g. myserver.database.windows.net" />
              </Field>
              <Field label="Database">
                <Input value={form.database} onChange={(_, d) => { setForm((prev) => ({ ...prev, database: d.value })); setTestResult(null); }} placeholder="e.g. MyDatabase" />
              </Field>
              <Field label="Port">
                <Input value={form.port} onChange={(_, d) => { setForm((prev) => ({ ...prev, port: d.value })); setTestResult(null); }} type="number" />
              </Field>
              <Field label="Authentication">
                <Switch
                  checked={form.useSqlAuth}
                  onChange={(_, d) => { setForm((prev) => ({ ...prev, useSqlAuth: d.checked })); setTestResult(null); }}
                  label="SQL Authentication"
                />
              </Field>
              {form.useSqlAuth && (
                <>
                  <Field label="Username">
                    <Input value={form.username} onChange={(_, d) => { setForm((prev) => ({ ...prev, username: d.value })); setTestResult(null); }} />
                  </Field>
                  <Field label="Password">
                    <Input value={form.password} onChange={(_, d) => { setForm((prev) => ({ ...prev, password: d.value })); setTestResult(null); }} type="password" />
                  </Field>
                </>
              )}
              <div className={styles.actions}>
                <Button appearance="primary" onClick={testConnection} disabled={testing || !form.server || !form.database}>
                  {testing ? 'Testing…' : 'Test Connection'}
                </Button>
                <Button appearance="primary" onClick={() => setStep('assess')} disabled={!canProceedToAssess}>
                  Next →
                </Button>
              </div>
              {testResult && (
                <div className={styles.fullWidth}>
                  <MessageBar intent={testResult.ok ? 'success' : 'error'}>
                    <MessageBarBody>{testResult.message}</MessageBarBody>
                  </MessageBar>
                </div>
              )}
            </div>
          </>
        )}

        {step === 'assess' && (
          <div className={styles.assessCenter}>
            {running ? (
              <>
                <Spinner size="medium" />
                <Text>Running assessment — this may take a few minutes…</Text>
              </>
            ) : (
              <>
                <Text as="h3" size={500} weight="semibold">Run Assessment</Text>
                <Text>Analyze the source database for schema, data, performance, and compatibility with Azure SQL.</Text>
                {error && (
                  <MessageBar intent="error">
                    <MessageBarBody>{error}</MessageBarBody>
                  </MessageBar>
                )}
                <div className={styles.actions} style={{ justifyContent: 'center', gridColumn: 'unset' }}>
                  <Button appearance="secondary" onClick={() => setStep('connect')}>
                    ← Back
                  </Button>
                  <Button appearance="primary" onClick={runAssessment}>
                    Run Assessment
                  </Button>
                </div>
              </>
            )}
          </div>
        )}

        {step === 'review' && report && (
          <>
            <Text as="h3" size={500} weight="semibold">Assessment Results</Text>
            <AssessmentReport report={report} projectId={id!} />
            <div className={styles.reviewActions}>
              <Button appearance="secondary" onClick={() => setStep('assess')}>
                ← Re-run
              </Button>
              <Button appearance="primary" onClick={() => navigate(`/projects/${id}`)}>
                Done
              </Button>
            </div>
          </>
        )}
      </Card>
    </div>
  );
}
