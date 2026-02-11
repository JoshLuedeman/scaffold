import { useState, useEffect, useRef } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useMsal } from '@azure/msal-react';
import type { PublicClientApplication } from '@azure/msal-browser';
import { api } from '../services/api';
import { useMigrationProgress } from '../hooks/useMigrationProgress';
import type { MigrationProject, MigrationResult, ValidationResult } from '../types';
import './MigrationExecution.css';

type MigrationStrategy = 'Cutover' | 'ContinuousSync';

export default function MigrationExecution() {
  const { id } = useParams<{ id: string }>();
  const { instance } = useMsal();
  const msalInstance = instance as PublicClientApplication;

  const [project, setProject] = useState<MigrationProject | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [migrationId, setMigrationId] = useState<string | null>(null);
  const [starting, setStarting] = useState(false);
  const [cutoverPending, setCutoverPending] = useState(false);
  const [validating, setValidating] = useState(false);
  const [result, setResult] = useState<MigrationResult | null>(null);

  const { progress, connectionStatus, log, migrationStatus } =
    useMigrationProgress(migrationId, msalInstance);

  const logEndRef = useRef<HTMLDivElement>(null);

  const strategy: MigrationStrategy | undefined = project?.migrationPlan?.strategy;

  useEffect(() => {
    api
      .get<MigrationProject>(`/projects/${id}`)
      .then(setProject)
      .catch((err: unknown) => {
        setError(err instanceof Error ? err.message : 'Failed to load project');
      })
      .finally(() => setLoading(false));
  }, [id]);

  // Auto-scroll log
  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [log]);

  // Fetch result on completion
  useEffect(() => {
    if (migrationStatus === 'completed' && migrationId) {
      api.get<MigrationResult>(`/projects/${id}/migrations/${migrationId}`).then(setResult).catch(() => {});
    }
  }, [migrationStatus, migrationId, id]);

  async function startMigration() {
    setStarting(true);
    setError(null);
    try {
      const res = await api.post<{ id: string }>(`/projects/${id}/migrations/start`, {});
      setMigrationId(res.id);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to start migration');
    } finally {
      setStarting(false);
    }
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

  if (loading) return <p>Loading…</p>;
  if (error && !migrationId) return <p className="feedback feedback-error">{error}</p>;
  if (!project) return null;

  return (
    <div className="migration-execution">
      <nav className="breadcrumb">
        <Link to="/">Projects</Link> <span>/</span>{' '}
        <Link to={`/projects/${id}`}>Project {id}</Link> <span>/</span>{' '}
        <span>Execute Migration</span>
      </nav>

      <h2>Execute Migration</h2>

      {/* Start */}
      {!migrationId && (
        <div className="exec-section">
          <p>
            Strategy: <strong>{strategy ?? 'N/A'}</strong>
          </p>
          <button className="btn-primary" onClick={startMigration} disabled={starting}>
            {starting ? 'Starting…' : 'Start Migration'}
          </button>
        </div>
      )}

      {/* Connection indicator */}
      {migrationId && (
        <div className="exec-connection">
          <span className={`conn-dot conn-${connectionStatus}`} />
          <span className="conn-label">{connectionStatus}</span>
        </div>
      )}

      {/* Status indicator */}
      {migrationId && (
        <div className={`exec-status exec-status-${migrationStatus}`}>
          {migrationStatus === 'running' && <span className="status-icon pulse">●</span>}
          {migrationStatus === 'completed' && <span className="status-icon">✓</span>}
          {migrationStatus === 'failed' && <span className="status-icon">✕</span>}
          {migrationStatus === 'idle' && <span className="status-icon">○</span>}
          <span className="status-label">{migrationStatus}</span>
        </div>
      )}

      {/* Progress */}
      {progress && (
        <div className="exec-section">
          <h3>Progress</h3>
          <div className="progress-phase">{progress.phase}</div>
          <div className="progress-bar-track">
            <div
              className="progress-bar-fill"
              style={{ width: `${progress.percentComplete}%` }}
            />
          </div>
          <div className="progress-meta">
            <span>{progress.percentComplete}%</span>
            <span>Table: {progress.currentTable || '—'}</span>
            <span>Rows: {progress.rowsProcessed.toLocaleString()}</span>
          </div>
        </div>
      )}

      {/* Cutover button for continuous sync */}
      {strategy === 'ContinuousSync' && migrationStatus === 'running' && (
        <div className="exec-section">
          <button
            className="btn-cutover"
            onClick={triggerCutover}
            disabled={cutoverPending}
          >
            {cutoverPending ? 'Triggering Cutover…' : 'Trigger Cutover'}
          </button>
        </div>
      )}

      {/* Message log */}
      {migrationId && (
        <div className="exec-section">
          <h3>Log</h3>
          <div className="exec-log">
            {log.map((entry, i) => (
              <div key={i} className="log-entry">
                <span className="log-time">
                  {entry.timestamp.toLocaleTimeString()}
                </span>
                <span className="log-msg">{entry.message}</span>
              </div>
            ))}
            <div ref={logEndRef} />
          </div>
        </div>
      )}

      {/* Error display */}
      {error && migrationId && (
        <p className="feedback feedback-error">{error}</p>
      )}

      {/* Validation */}
      {migrationStatus === 'completed' && !result && (
        <div className="exec-section">
          <button className="btn-primary" onClick={runValidation} disabled={validating}>
            {validating ? 'Validating…' : 'Run Validation'}
          </button>
        </div>
      )}

      {/* Validation results */}
      {result && (
        <div className="exec-section">
          <h3>Validation Results</h3>
          <p className={`validation-summary ${result.success ? 'validation-pass' : 'validation-fail'}`}>
            {result.success ? 'All validations passed' : 'Some validations failed'}
          </p>
          <table className="validation-table">
            <thead>
              <tr>
                <th>Table</th>
                <th>Source Rows</th>
                <th>Target Rows</th>
                <th>Checksum</th>
                <th>Result</th>
              </tr>
            </thead>
            <tbody>
              {result.validations.map((v: ValidationResult) => (
                <tr key={v.tableName} className={v.passed ? 'row-pass' : 'row-fail'}>
                  <td>{v.tableName}</td>
                  <td>{v.sourceRowCount.toLocaleString()}</td>
                  <td>{v.targetRowCount.toLocaleString()}</td>
                  <td>{v.checksumMatch ? '✓' : '✕'}</td>
                  <td>
                    <span className={`badge ${v.passed ? 'badge-pass' : 'badge-fail'}`}>
                      {v.passed ? 'Pass' : 'Fail'}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
