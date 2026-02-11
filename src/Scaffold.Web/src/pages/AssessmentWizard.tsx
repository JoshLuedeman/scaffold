import { useState } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { api } from '../services/api';
import type { AssessmentReport as Report } from '../types';
import AssessmentReport from '../components/AssessmentReport';
import './AssessmentWizard.css';

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

  const stepIndex = STEPS.findIndex((s) => s.key === step);

  function stepClass(i: number) {
    if (i < stepIndex) return 'wizard-step completed';
    if (i === stepIndex) return 'wizard-step active';
    return 'wizard-step';
  }

  function update(field: keyof ConnectionForm) {
    return (e: React.ChangeEvent<HTMLInputElement>) => {
      const value = field === 'useSqlAuth' ? e.target.checked : e.target.value;
      setForm((prev) => ({ ...prev, [field]: value }));
      setTestResult(null);
    };
  }

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

  return (
    <div className="assessment-wizard">
      <nav className="breadcrumb">
        <Link to="/">Projects</Link> <span>/</span>{' '}
        <Link to={`/projects/${id}`}>Project {id}</Link> <span>/</span>{' '}
        <span>Assessment</span>
      </nav>

      <div className="wizard-header">
        <h2>Assessment Wizard</h2>
      </div>

      <div className="wizard-steps">
        {STEPS.map((s, i) => (
          <div key={s.key}>
            {i > 0 && <span className="wizard-step-divider" />}
            <span className={stepClass(i)}>
              <span className="step-number">{i + 1}</span>
              {s.label}
            </span>
          </div>
        ))}
      </div>

      <div className="wizard-panel">
        {step === 'connect' && (
          <>
            <h3>Source Database Connection</h3>
            <div className="connection-form">
              <div className="form-group">
                <label>Server</label>
                <input value={form.server} onChange={update('server')} placeholder="e.g. myserver.database.windows.net" />
              </div>
              <div className="form-group">
                <label>Database</label>
                <input value={form.database} onChange={update('database')} placeholder="e.g. MyDatabase" />
              </div>
              <div className="form-group">
                <label>Port</label>
                <input value={form.port} onChange={update('port')} type="number" />
              </div>
              <div className="form-group">
                <label>Authentication</label>
                <div className="auth-toggle">
                  <input type="checkbox" checked={form.useSqlAuth} onChange={update('useSqlAuth')} id="sqlAuth" />
                  <label htmlFor="sqlAuth" style={{ margin: 0, textTransform: 'none', letterSpacing: 'normal', fontWeight: 'normal' }}>
                    SQL Authentication
                  </label>
                </div>
              </div>
              {form.useSqlAuth && (
                <>
                  <div className="form-group">
                    <label>Username</label>
                    <input value={form.username} onChange={update('username')} />
                  </div>
                  <div className="form-group">
                    <label>Password</label>
                    <input value={form.password} onChange={update('password')} type="password" />
                  </div>
                </>
              )}
              <div className="form-actions">
                <button className="btn-primary" onClick={testConnection} disabled={testing || !form.server || !form.database}>
                  {testing ? 'Testing…' : 'Test Connection'}
                </button>
                <button className="btn-primary" onClick={() => setStep('assess')} disabled={!canProceedToAssess}>
                  Next →
                </button>
                {testResult && (
                  <span className={`feedback ${testResult.ok ? 'feedback-success' : 'feedback-error'}`}>
                    {testResult.message}
                  </span>
                )}
              </div>
            </div>
          </>
        )}

        {step === 'assess' && (
          <div className="assess-step">
            {running ? (
              <div className="assess-running">
                <div className="spinner" />
                <p>Running assessment — this may take a few minutes…</p>
              </div>
            ) : (
              <>
                <h3>Run Assessment</h3>
                <p>Analyze the source database for schema, data, performance, and compatibility with Azure SQL.</p>
                {error && <p className="feedback feedback-error">{error}</p>}
                <div className="form-actions" style={{ justifyContent: 'center' }}>
                  <button className="btn-secondary" onClick={() => setStep('connect')}>
                    ← Back
                  </button>
                  <button className="btn-primary" onClick={runAssessment}>
                    Run Assessment
                  </button>
                </div>
              </>
            )}
          </div>
        )}

        {step === 'review' && report && (
          <>
            <h3>Assessment Results</h3>
            <AssessmentReport report={report} />
            <div className="review-actions">
              <button className="btn-secondary" onClick={() => setStep('assess')}>
                ← Re-run
              </button>
              <button className="btn-primary" onClick={() => navigate(`/projects/${id}`)}>
                Done
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
