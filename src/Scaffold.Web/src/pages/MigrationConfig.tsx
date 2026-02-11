import { useState, useEffect, useMemo } from 'react';
import { useParams, Link } from 'react-router-dom';
import { api } from '../services/api';
import type {
  MigrationProject,
  MigrationPlan,
  MigrationStrategy,
  SchemaObject,
  TierRecommendation,
} from '../types';
import './MigrationConfig.css';

const COMMON_TIERS = [
  'Basic',
  'Standard S0',
  'Standard S1',
  'Standard S2',
  'Standard S3',
  'Premium P1',
  'Premium P2',
  'General Purpose (2 vCores)',
  'General Purpose (4 vCores)',
  'General Purpose (8 vCores)',
  'Business Critical (4 vCores)',
  'Business Critical (8 vCores)',
  'Hyperscale (2 vCores)',
];

const STRATEGY_INFO: Record<MigrationStrategy, { label: string; description: string }> = {
  Cutover: {
    label: 'Cutover',
    description:
      'One-time migration with a maintenance window. Source database is taken offline, data is migrated, then traffic switches to the target.',
  },
  ContinuousSync: {
    label: 'Continuous Sync',
    description:
      'Ongoing replication keeps source and target in sync. Allows near-zero downtime cutover when ready.',
  },
};

function objectKey(obj: SchemaObject) {
  return `${obj.schema}.${obj.name}`;
}

export default function MigrationConfig() {
  const { id } = useParams<{ id: string }>();

  const [project, setProject] = useState<MigrationProject | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [strategy, setStrategy] = useState<MigrationStrategy>('Cutover');
  const [selectedObjects, setSelectedObjects] = useState<Set<string>>(new Set());
  const [tierOverride, setTierOverride] = useState('');
  const [scheduleNow, setScheduleNow] = useState(true);
  const [scheduledAt, setScheduledAt] = useState('');
  const [preMigrationScript, setPreMigrationScript] = useState('');
  const [postMigrationScript, setPostMigrationScript] = useState('');

  // Save/approve state
  const [saving, setSaving] = useState(false);
  const [savedPlan, setSavedPlan] = useState<MigrationPlan | null>(null);
  const [approving, setApproving] = useState(false);
  const [feedback, setFeedback] = useState<{ ok: boolean; message: string } | null>(null);

  const allObjects: SchemaObject[] = useMemo(
    () => project?.assessment?.schema.objects ?? [],
    [project],
  );

  useEffect(() => {
    async function load() {
      try {
        const p = await api.get<MigrationProject>(`/projects/${id}`);
        setProject(p);
        // Pre-select all objects
        if (p.assessment?.schema.objects) {
          setSelectedObjects(new Set(p.assessment.schema.objects.map(objectKey)));
        }
        // Load existing plan if any
        if (p.migrationPlan) {
          const plan = p.migrationPlan;
          setStrategy(plan.strategy);
          setPreMigrationScript(plan.preMigrationScript ?? '');
          setPostMigrationScript(plan.postMigrationScript ?? '');
          if (plan.scheduledAt) {
            setScheduleNow(false);
            setScheduledAt(plan.scheduledAt);
          }
          setSavedPlan(plan);
          if (plan.excludedObjects.length > 0 && p.assessment?.schema.objects) {
            const excluded = new Set(plan.excludedObjects);
            setSelectedObjects(
              new Set(
                p.assessment.schema.objects
                  .map(objectKey)
                  .filter((k) => !excluded.has(k)),
              ),
            );
          }
        }
      } catch (err: unknown) {
        const message = err instanceof Error ? err.message : 'Failed to load project';
        setError(message);
      } finally {
        setLoading(false);
      }
    }
    load();
  }, [id]);

  const recommendation: TierRecommendation | undefined = project?.assessment?.recommendation;

  function toggleObject(key: string) {
    setSelectedObjects((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  function selectAll() {
    setSelectedObjects(new Set(allObjects.map(objectKey)));
  }

  function deselectAll() {
    setSelectedObjects(new Set());
  }

  async function savePlan() {
    setSaving(true);
    setFeedback(null);
    try {
      const included = allObjects.filter((o) => selectedObjects.has(objectKey(o))).map(objectKey);
      const excluded = allObjects.filter((o) => !selectedObjects.has(objectKey(o))).map(objectKey);

      const targetTier: TierRecommendation = tierOverride
        ? { ...recommendation!, serviceTier: tierOverride, reasoning: 'User override' }
        : recommendation!;

      const body = {
        strategy,
        includedObjects: included,
        excludedObjects: excluded,
        scheduledAt: scheduleNow ? undefined : scheduledAt || undefined,
        preMigrationScript: preMigrationScript || undefined,
        postMigrationScript: postMigrationScript || undefined,
        targetTier,
        useExistingTarget: false,
      };

      const plan = savedPlan
        ? await api.put<MigrationPlan>(`/projects/${id}/migration-plans`, body)
        : await api.post<MigrationPlan>(`/projects/${id}/migration-plans`, body);
      setSavedPlan(plan);
      setFeedback({ ok: true, message: 'Plan saved' });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to save plan';
      setFeedback({ ok: false, message });
    } finally {
      setSaving(false);
    }
  }

  async function approvePlan() {
    if (!savedPlan) return;
    setApproving(true);
    setFeedback(null);
    try {
      const approved = await api.post<MigrationPlan>(
        `/projects/${id}/migration-plans/approve`,
        {},
      );
      setSavedPlan(approved);
      setFeedback({ ok: true, message: 'Plan approved' });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Failed to approve plan';
      setFeedback({ ok: false, message });
    } finally {
      setApproving(false);
    }
  }

  if (loading) return <p>Loading…</p>;
  if (error) return <p className="feedback feedback-error">{error}</p>;
  if (!project) return null;

  return (
    <div className="migration-config">
      <nav className="breadcrumb">
        <Link to="/">Projects</Link> <span>/</span>{' '}
        <Link to={`/projects/${id}`}>Project {id}</Link> <span>/</span>{' '}
        <span>Configure Migration</span>
      </nav>

      <h2>Configure Migration</h2>

      {/* Strategy */}
      <div className="config-section">
        <h3>Migration Strategy</h3>
        <div className="strategy-options">
          {(Object.keys(STRATEGY_INFO) as MigrationStrategy[]).map((key) => (
            <label
              key={key}
              className={`strategy-option ${strategy === key ? 'selected' : ''}`}
            >
              <input
                type="radio"
                name="strategy"
                value={key}
                checked={strategy === key}
                onChange={() => setStrategy(key)}
              />
              <div className="strategy-label">
                <strong>{STRATEGY_INFO[key].label}</strong>
                <span>{STRATEGY_INFO[key].description}</span>
              </div>
            </label>
          ))}
        </div>
      </div>

      {/* Object Selection */}
      <div className="config-section">
        <h3>Schema Objects</h3>
        <div className="object-toolbar">
          <button className="btn-link" onClick={selectAll}>Select all</button>
          <button className="btn-link" onClick={deselectAll}>Deselect all</button>
        </div>
        <div className="object-list">
          {allObjects.length === 0 && (
            <p style={{ color: '#888', fontSize: '0.85rem', margin: '0.5rem' }}>
              No assessment data available. Run an assessment first.
            </p>
          )}
          {allObjects.map((obj) => {
            const key = objectKey(obj);
            return (
              <label key={key} className="object-item">
                <input
                  type="checkbox"
                  checked={selectedObjects.has(key)}
                  onChange={() => toggleObject(key)}
                />
                <span className="object-type-badge">{obj.objectType}</span>
                <span>{key}</span>
              </label>
            );
          })}
        </div>
        <p className="object-count">
          {selectedObjects.size} of {allObjects.length} objects selected
        </p>
      </div>

      {/* Target Tier */}
      <div className="config-section">
        <h3>Target Tier</h3>
        {recommendation && (
          <p className="tier-current">
            Recommended: <strong>{recommendation.serviceTier} — {recommendation.computeSize}</strong>{' '}
            (~${recommendation.estimatedMonthlyCostUsd}/mo)
          </p>
        )}
        <div className="tier-override">
          <label htmlFor="tierOverride">Override</label>
          <select
            id="tierOverride"
            value={tierOverride}
            onChange={(e) => setTierOverride(e.target.value)}
          >
            <option value="">Use recommended</option>
            {COMMON_TIERS.map((t) => (
              <option key={t} value={t}>{t}</option>
            ))}
          </select>
        </div>
      </div>

      {/* Schedule */}
      <div className="config-section">
        <h3>Schedule</h3>
        <div className="schedule-options">
          <label className="schedule-option">
            <input
              type="radio"
              name="schedule"
              checked={scheduleNow}
              onChange={() => setScheduleNow(true)}
            />
            Migrate now (on approval)
          </label>
          <label className="schedule-option">
            <input
              type="radio"
              name="schedule"
              checked={!scheduleNow}
              onChange={() => setScheduleNow(false)}
            />
            Schedule for:
            {!scheduleNow && (
              <input
                type="datetime-local"
                value={scheduledAt}
                onChange={(e) => setScheduledAt(e.target.value)}
              />
            )}
          </label>
        </div>
      </div>

      {/* Scripts */}
      <div className="config-section">
        <h3>Migration Scripts (Optional)</h3>
        <div className="script-group">
          <label htmlFor="preScript">Pre-migration SQL</label>
          <textarea
            id="preScript"
            value={preMigrationScript}
            onChange={(e) => setPreMigrationScript(e.target.value)}
            placeholder="SQL to run before migration begins…"
          />
        </div>
        <div className="script-group">
          <label htmlFor="postScript">Post-migration SQL</label>
          <textarea
            id="postScript"
            value={postMigrationScript}
            onChange={(e) => setPostMigrationScript(e.target.value)}
            placeholder="SQL to run after migration completes…"
          />
        </div>
      </div>

      {/* Save */}
      <div className="config-actions">
        <button className="btn-primary" onClick={savePlan} disabled={saving || selectedObjects.size === 0}>
          {saving ? 'Saving…' : 'Save Plan'}
        </button>
        {feedback && (
          <span className={`feedback ${feedback.ok ? 'feedback-success' : 'feedback-error'}`}>
            {feedback.message}
          </span>
        )}
      </div>

      {/* Review & Approve */}
      {savedPlan && (
        <div className="config-section approve-section">
          <h3>Review &amp; Approve</h3>
          <dl className="plan-summary">
            <dt>Strategy</dt>
            <dd>{STRATEGY_INFO[savedPlan.strategy].label}</dd>
            <dt>Objects</dt>
            <dd>{savedPlan.includedObjects.length} included, {savedPlan.excludedObjects.length} excluded</dd>
            <dt>Target</dt>
            <dd>{savedPlan.targetTier.serviceTier} — {savedPlan.targetTier.computeSize}</dd>
            <dt>Schedule</dt>
            <dd>{savedPlan.scheduledAt ? new Date(savedPlan.scheduledAt).toLocaleString() : 'Immediately on approval'}</dd>
            {savedPlan.preMigrationScript && <><dt>Pre-script</dt><dd>Provided</dd></>}
            {savedPlan.postMigrationScript && <><dt>Post-script</dt><dd>Provided</dd></>}
            <dt>Status</dt>
            <dd>{savedPlan.isApproved ? `Approved by ${savedPlan.approvedBy}` : 'Pending approval'}</dd>
          </dl>
          <div className="approve-actions">
            <button
              className="btn-approve"
              onClick={approvePlan}
              disabled={approving || savedPlan.isApproved}
            >
              {approving ? 'Approving…' : savedPlan.isApproved ? 'Approved ✓' : 'Approve'}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
