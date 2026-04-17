import { useState, useEffect, useMemo } from 'react';
import { useParams, Link } from 'react-router-dom';
import { api } from '../../services/api';
import type {
  MigrationProject,
  MigrationPlan,
  MigrationStrategy,
  MigrationScript,
  SchemaObject,
  TierRecommendation,
  RegionPricing,
  DatabasePlatform,
} from '../../types';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbDivider,
  Button,
  Card,
  CardHeader,
  Checkbox,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  DialogTrigger,
  Field,
  Input,
  MessageBar,
  MessageBarBody,
  Radio,
  RadioGroup,
  Spinner,
  Text,
  Textarea,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import type { CannedScriptInfo } from './types';
import { objectKey } from './types';
import { StrategySection } from './StrategySection';
import { ObjectSelectionSection } from './ObjectSelectionSection';
import { TargetTierSection } from './TargetTierSection';
import { RegionPricingSection } from './RegionPricingSection';
import { ScriptSection } from './ScriptSection';
import { ReviewApproveSection } from './ReviewApproveSection';

const useStyles = makeStyles({
  root: {
    maxWidth: '800px',
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  card: {
    padding: tokens.spacingHorizontalXL,
  },
  cardTitle: {
    fontWeight: tokens.fontWeightSemibold,
  },
  scheduleInput: {
    marginLeft: tokens.spacingHorizontalS,
  },
  actionsRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    alignItems: 'center',
  },
});

export default function MigrationConfig() {
  const { id } = useParams<{ id: string }>();

  const [project, setProject] = useState<MigrationProject | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [strategy, setStrategy] = useState<MigrationStrategy>('Cutover');
  const [selectedObjects, setSelectedObjects] = useState<Set<string>>(new Set());
  const [serviceOverride, setServiceOverride] = useState('');
  const [tierOverride, setTierOverride] = useState('');
  const [scheduleNow, setScheduleNow] = useState(true);
  const [scheduledAt, setScheduledAt] = useState('');
  const [availableScripts, setAvailableScripts] = useState<CannedScriptInfo[]>([]);
  const [selectedScripts, setSelectedScripts] = useState<Set<string>>(new Set());
  const [customScripts, setCustomScripts] = useState<MigrationScript[]>([]);
  const [previewScript, setPreviewScript] = useState<{ scriptId: string; sql: string } | null>(null);
  const [showPreview, setShowPreview] = useState(false);
  const [showCustomDialog, setShowCustomDialog] = useState(false);
  const [customDialogPhase, setCustomDialogPhase] = useState<'Pre' | 'Post'>('Pre');
  const [customScriptLabel, setCustomScriptLabel] = useState('');
  const [customScriptContent, setCustomScriptContent] = useState('');

  // Target connection state
  const [useExistingTarget, setUseExistingTarget] = useState(false);
  const [targetServer, setTargetServer] = useState('');
  const [targetDatabase, setTargetDatabase] = useState('');
  const [targetUsername, setTargetUsername] = useState('');
  const [targetPassword, setTargetPassword] = useState('');
  const [testingTarget, setTestingTarget] = useState(false);
  const [targetTestResult, setTargetTestResult] = useState<{ ok: boolean; message: string } | null>(null);

  // Region pricing state
  const [regionPricing, setRegionPricing] = useState<RegionPricing[]>([]);
  const [selectedRegion, setSelectedRegion] = useState('');
  const [loadingPricing, setLoadingPricing] = useState(false);

  // Filter state
  const [typeFilter, setTypeFilter] = useState('');
  const [nameFilter, setNameFilter] = useState('');

  // Save/approve state
  const [saving, setSaving] = useState(false);
  const [savedPlan, setSavedPlan] = useState<MigrationPlan | null>(null);
  const [approving, setApproving] = useState(false);
  const [feedback, setFeedback] = useState<{ ok: boolean; message: string } | null>(null);

  // Derive platform from project
  const sourcePlatform: DatabasePlatform | undefined =
    project?.migrationPlan?.sourcePlatform ?? project?.sourceConnection?.platform;
  const targetPlatform: DatabasePlatform | undefined =
    project?.migrationPlan?.targetPlatform ?? sourcePlatform;

  const allObjects: SchemaObject[] = useMemo(
    () => project?.assessment?.schema.objects ?? [],
    [project],
  );

  const objectTypes = useMemo(
    () => [...new Set(allObjects.map((o) => o.objectType))].sort(),
    [allObjects],
  );

  const filteredObjects = useMemo(() => {
    return allObjects.filter((obj) => {
      const matchesType = !typeFilter || obj.objectType === typeFilter;
      const matchesName = !nameFilter || objectKey(obj).toLowerCase().includes(nameFilter.toLowerCase());
      return matchesType && matchesName;
    });
  }, [allObjects, typeFilter, nameFilter]);

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
          if (plan.preMigrationScripts?.length > 0 || plan.postMigrationScripts?.length > 0) {
            const allScripts = [...(plan.preMigrationScripts || []), ...(plan.postMigrationScripts || [])];
            const cannedIds = new Set(allScripts.filter(s => s.scriptType === 'Canned' && s.isEnabled).map(s => s.scriptId));
            setSelectedScripts(cannedIds);
            setCustomScripts(allScripts.filter(s => s.scriptType === 'Custom'));
          }
          if (plan.scheduledAt) {
            setScheduleNow(false);
            setScheduledAt(plan.scheduledAt);
          }
          setSavedPlan(plan);
          if (
            p.assessment?.recommendation &&
            plan.targetTier.serviceTier !== p.assessment.recommendation.serviceTier
          ) {
            setServiceOverride(plan.targetTier.serviceTier);
            setTierOverride(plan.targetTier.computeSize);
          }
          if (plan.targetRegion) {
            setSelectedRegion(plan.targetRegion);
          }
          if (plan.useExistingTarget) {
            setUseExistingTarget(true);
          }
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

  useEffect(() => {
    if (!id) return;
    api.get<CannedScriptInfo[]>(`/projects/${id}/migration-scripts/available`)
      .then(setAvailableScripts)
      .catch(() => {});
  }, [id]);

  useEffect(() => {
    async function fetchPricing() {
      if (!project?.assessment?.recommendation) return;
      const rec = project.assessment.recommendation;
      const service = serviceOverride || rec.serviceTier;
      const compute = (serviceOverride ? tierOverride : '') || rec.computeSize;
      const storageGb = rec.storageGb;

      setLoadingPricing(true);
      try {
        const data = await api.get<RegionPricing[]>(
          `/pricing/estimate?service=${encodeURIComponent(service)}&compute=${encodeURIComponent(compute)}&storageGb=${storageGb}`,
        );
        setRegionPricing(data);
        if (!selectedRegion) {
          if (rec.recommendedRegion) {
            setSelectedRegion(rec.recommendedRegion);
          } else if (data.length > 0) {
            setSelectedRegion(data[0].armRegionName);
          }
        }
      } catch {
        setRegionPricing([]);
      } finally {
        setLoadingPricing(false);
      }
    }
    fetchPricing();
  }, [project, serviceOverride, tierOverride]); // eslint-disable-line react-hooks/exhaustive-deps

  const recommendation: TierRecommendation | undefined = project?.assessment?.recommendation;
  const styles = useStyles();

  function toggleObject(key: string) {
    setSelectedObjects((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  function selectAll() {
    setSelectedObjects((prev) => {
      const next = new Set(prev);
      for (const obj of filteredObjects) next.add(objectKey(obj));
      return next;
    });
  }

  function deselectAll() {
    setSelectedObjects((prev) => {
      const next = new Set(prev);
      for (const obj of filteredObjects) next.delete(objectKey(obj));
      return next;
    });
  }

  const toggleScript = (scriptId: string) => {
    setSelectedScripts(prev => {
      const next = new Set(prev);
      if (next.has(scriptId)) next.delete(scriptId);
      else next.add(scriptId);
      return next;
    });
  };

  const previewCannedScript = async (scriptId: string) => {
    try {
      const data = await api.get<{ scriptId: string; sql: string }>(`/projects/${id}/migration-scripts/preview?scriptId=${scriptId}`);
      setPreviewScript(data);
      setShowPreview(true);
    } catch { /* ignore */ }
  };

  const addCustomScript = () => {
    if (!customScriptLabel.trim() || !customScriptContent.trim()) return;
    const script: MigrationScript = {
      scriptId: `custom-${Date.now()}`,
      label: customScriptLabel,
      scriptType: 'Custom',
      phase: customDialogPhase,
      sqlContent: customScriptContent,
      isEnabled: true,
      order: customScripts.length,
    };
    setCustomScripts(prev => [...prev, script]);
    setCustomScriptLabel('');
    setCustomScriptContent('');
    setShowCustomDialog(false);
  };

  const removeCustomScript = (scriptId: string) => {
    setCustomScripts(prev => prev.filter(s => s.scriptId !== scriptId));
  };

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => setCustomScriptContent(reader.result as string);
    reader.readAsText(file);
  };

  async function testTargetConnection() {
    setTestingTarget(true);
    setTargetTestResult(null);
    try {
      await api.post('/connections/test', {
        server: targetServer,
        database: targetDatabase,
        useSqlAuthentication: true,
        username: targetUsername,
        password: targetPassword,
        trustServerCertificate: true,
      });
      setTargetTestResult({ ok: true, message: 'Connection successful' });
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Connection failed';
      setTargetTestResult({ ok: false, message });
    } finally {
      setTestingTarget(false);
    }
  }

  function buildTargetConnectionString(): string {
    const parts = [
      `Server=${targetServer}`,
      `Database=${targetDatabase}`,
      `User Id=${targetUsername}`,
      `Password=${targetPassword}`,
      'TrustServerCertificate=True',
      'Encrypt=True',
    ];
    return parts.join(';');
  }

  async function savePlan() {
    setSaving(true);
    setFeedback(null);
    try {
      const included = allObjects.filter((o) => selectedObjects.has(objectKey(o))).map(objectKey);
      const excluded = allObjects.filter((o) => !selectedObjects.has(objectKey(o))).map(objectKey);

      const targetTierOverride = serviceOverride
        ? {
            serviceTier: serviceOverride,
            computeSize: tierOverride || recommendation!.computeSize,
            dtus: recommendation!.dtus,
            vCores: recommendation!.vCores,
            storageGb: recommendation!.storageGb,
            estimatedMonthlyCostUsd: recommendation!.estimatedMonthlyCostUsd,
            reasoning: 'User override',
          }
        : {
            serviceTier: recommendation!.serviceTier,
            computeSize: recommendation!.computeSize,
            dtus: recommendation!.dtus,
            vCores: recommendation!.vCores,
            storageGb: recommendation!.storageGb,
            estimatedMonthlyCostUsd: recommendation!.estimatedMonthlyCostUsd,
            reasoning: recommendation!.reasoning,
          };

      const preScripts: MigrationScript[] = [
        ...availableScripts
          .filter(s => s.phase === 'Pre' && selectedScripts.has(s.scriptId))
          .map((s, i) => ({ scriptId: s.scriptId, label: s.label, scriptType: 'Canned' as const, phase: 'Pre' as const, sqlContent: '', isEnabled: true, order: i })),
        ...customScripts.filter(s => s.phase === 'Pre'),
      ];
      const postScripts: MigrationScript[] = [
        ...availableScripts
          .filter(s => s.phase === 'Post' && selectedScripts.has(s.scriptId))
          .map((s, i) => ({ scriptId: s.scriptId, label: s.label, scriptType: 'Canned' as const, phase: 'Post' as const, sqlContent: '', isEnabled: true, order: i })),
        ...customScripts.filter(s => s.phase === 'Post'),
      ];

      const body = {
        strategy,
        includedObjects: included,
        excludedObjects: excluded,
        scheduledAt: scheduleNow ? undefined : scheduledAt || undefined,
        preMigrationScripts: preScripts,
        postMigrationScripts: postScripts,
        targetTierOverride,
        targetRegion: selectedRegion || undefined,
        useExistingTarget,
        existingTargetConnectionString: useExistingTarget ? buildTargetConnectionString() : undefined,
        sourcePlatform,
        targetPlatform,
      };

      const plan = savedPlan
        ? await api.put<MigrationPlan>(`/projects/${id}/migration-plans/${savedPlan.id}`, body)
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
        `/projects/${id}/migration-plans/${savedPlan.id}/approve`,
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

  if (loading) return <Spinner label="Loading…" />;
  if (error) return <MessageBar intent="error"><MessageBarBody>{error}</MessageBarBody></MessageBar>;
  if (!project) return null;

  return (
    <div className={styles.root}>
      <Breadcrumb>
        <BreadcrumbItem><Link to="/">Projects</Link></BreadcrumbItem>
        <BreadcrumbDivider />
        <BreadcrumbItem><Link to={`/projects/${id}`}>Project {id}</Link></BreadcrumbItem>
        <BreadcrumbDivider />
        <BreadcrumbItem>Configure Migration</BreadcrumbItem>
      </Breadcrumb>

      <Text as="h2" size={600} weight="semibold">Configure Migration</Text>

      <StrategySection
        strategy={strategy}
        onStrategyChange={setStrategy}
        sourcePlatform={sourcePlatform}
      />

      <ObjectSelectionSection
        allObjects={allObjects}
        filteredObjects={filteredObjects}
        selectedObjects={selectedObjects}
        objectTypes={objectTypes}
        typeFilter={typeFilter}
        nameFilter={nameFilter}
        onTypeFilterChange={setTypeFilter}
        onNameFilterChange={setNameFilter}
        onToggleObject={toggleObject}
        onSelectAll={selectAll}
        onDeselectAll={deselectAll}
      />

      <TargetTierSection
        recommendation={recommendation}
        regionPricing={regionPricing}
        serviceOverride={serviceOverride}
        tierOverride={tierOverride}
        onServiceOverrideChange={(value) => {
          setServiceOverride(value);
          setTierOverride('');
        }}
        onTierOverrideChange={setTierOverride}
        sourcePlatform={sourcePlatform}
      />

      <RegionPricingSection
        regionPricing={regionPricing}
        selectedRegion={selectedRegion}
        onRegionChange={setSelectedRegion}
        loadingPricing={loadingPricing}
      />

      {/* Target Database */}
      <Card className={styles.card}>
        <CardHeader header={<Text className={styles.cardTitle}>Target Database</Text>} />
        <Checkbox
          label={sourcePlatform === 'PostgreSql'
            ? 'Use an existing Azure Database for PostgreSQL as the migration target'
            : 'Use an existing Azure SQL database as the migration target'}
          checked={useExistingTarget}
          onChange={(_, data) => setUseExistingTarget(data.checked === true)}
        />
        {useExistingTarget && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginTop: '12px' }}>
            <Field label="Server">
              <Input
                placeholder={sourcePlatform === 'PostgreSql' ? 'myserver.postgres.database.azure.com' : 'myserver.database.windows.net'}
                value={targetServer}
                onChange={(_, d) => setTargetServer(d.value)}
              />
            </Field>
            <Field label="Database">
              <Input value={targetDatabase} onChange={(_, d) => setTargetDatabase(d.value)} />
            </Field>
            <Field label="Username">
              <Input value={targetUsername} onChange={(_, d) => setTargetUsername(d.value)} />
            </Field>
            <Field label="Password">
              <Input type="password" value={targetPassword} onChange={(_, d) => setTargetPassword(d.value)} />
            </Field>
            <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
              <Button appearance="secondary" onClick={testTargetConnection} disabled={testingTarget || !targetServer || !targetDatabase}>
                {testingTarget ? 'Testing...' : 'Test Connection'}
              </Button>
              {targetTestResult && (
                <MessageBar intent={targetTestResult.ok ? 'success' : 'error'}>
                  <MessageBarBody>{targetTestResult.message}</MessageBarBody>
                </MessageBar>
              )}
            </div>
          </div>
        )}
      </Card>

      {/* Schedule */}
      <Card className={styles.card}>
        <CardHeader header={<Text className={styles.cardTitle}>Schedule</Text>} />
        <RadioGroup
          value={scheduleNow ? 'now' : 'scheduled'}
          onChange={(_e, data) => setScheduleNow(data.value === 'now')}
        >
          <Radio value="now" label="Migrate now (on approval)" />
          <Radio value="scheduled" label="Schedule for" />
        </RadioGroup>
        {!scheduleNow && (
          <Input
            className={styles.scheduleInput}
            type="datetime-local"
            value={scheduledAt}
            onChange={(_e, data) => setScheduledAt(data.value)}
          />
        )}
      </Card>

      <ScriptSection
        phase="Pre"
        availableScripts={availableScripts}
        selectedScripts={selectedScripts}
        customScripts={customScripts}
        onToggleScript={toggleScript}
        onPreviewScript={previewCannedScript}
        onRemoveCustomScript={removeCustomScript}
        onAddCustomScript={(phase) => { setCustomDialogPhase(phase); setShowCustomDialog(true); }}
      />

      <ScriptSection
        phase="Post"
        availableScripts={availableScripts}
        selectedScripts={selectedScripts}
        customScripts={customScripts}
        onToggleScript={toggleScript}
        onPreviewScript={previewCannedScript}
        onRemoveCustomScript={removeCustomScript}
        onAddCustomScript={(phase) => { setCustomDialogPhase(phase); setShowCustomDialog(true); }}
      />

      {/* Save */}
      <div className={styles.actionsRow}>
        <Button
          appearance="primary"
          onClick={savePlan}
          disabled={saving || selectedObjects.size === 0}
        >
          {saving ? 'Saving…' : 'Save Plan'}
        </Button>
        {feedback && (
          <MessageBar intent={feedback.ok ? 'success' : 'error'}>
            <MessageBarBody>{feedback.message}</MessageBarBody>
          </MessageBar>
        )}
      </div>

      {/* Review & Approve */}
      {savedPlan && (
        <ReviewApproveSection
          savedPlan={savedPlan}
          approving={approving}
          onApprove={approvePlan}
          availableScripts={availableScripts}
          selectedScripts={selectedScripts}
          customScripts={customScripts}
        />
      )}

      {/* Custom Script Dialog */}
      <Dialog open={showCustomDialog} onOpenChange={(_e, data) => setShowCustomDialog(data.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Add Custom {customDialogPhase}-Migration Script</DialogTitle>
            <DialogContent>
              <Field label="Script Name" required style={{ marginBottom: tokens.spacingVerticalM }}>
                <Input value={customScriptLabel} onChange={(_e, data) => setCustomScriptLabel(data.value)} placeholder="e.g. Custom cleanup script" />
              </Field>
              <Field label="SQL Content" required>
                <Textarea
                  value={customScriptContent}
                  onChange={(_e, data) => setCustomScriptContent(data.value)}
                  placeholder="Paste SQL here..."
                  resize="vertical"
                  style={{ minHeight: '200px', fontFamily: 'monospace' }}
                />
              </Field>
              <div style={{ marginTop: tokens.spacingVerticalS }}>
                <input type="file" accept=".sql,.txt" onChange={handleFileUpload} />
              </div>
            </DialogContent>
            <DialogActions>
              <DialogTrigger disableButtonEnhancement>
                <Button appearance="secondary">Cancel</Button>
              </DialogTrigger>
              <Button appearance="primary" onClick={addCustomScript} disabled={!customScriptLabel.trim() || !customScriptContent.trim()}>
                Add Script
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      {/* Preview Dialog */}
      <Dialog open={showPreview} onOpenChange={(_e, data) => setShowPreview(data.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Script Preview: {previewScript?.scriptId}</DialogTitle>
            <DialogContent>
              <pre style={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: '13px', maxHeight: '400px', overflow: 'auto', background: tokens.colorNeutralBackground3, padding: tokens.spacingVerticalM, borderRadius: tokens.borderRadiusMedium }}>
                {previewScript?.sql}
              </pre>
            </DialogContent>
            <DialogActions>
              <DialogTrigger disableButtonEnhancement>
                <Button appearance="secondary">Close</Button>
              </DialogTrigger>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}