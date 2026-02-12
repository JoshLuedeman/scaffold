import { useState, useEffect, useMemo } from 'react';
import { useParams, Link } from 'react-router-dom';
import { api } from '../services/api';
import type {
  MigrationProject,
  MigrationPlan,
  MigrationStrategy,
  MigrationScript,
  SchemaObject,
  TierRecommendation,
  RegionPricing,
} from '../types';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbDivider,
  Card,
  CardHeader,
  Button,
  Text,
  Badge,
  RadioGroup,
  Radio,
  Checkbox,
  Select,
  Field,
  Textarea,
  Input,
  MessageBar,
  MessageBarBody,
  Spinner,
  Dialog,
  DialogSurface,
  DialogBody,
  DialogTitle,
  DialogContent,
  DialogActions,
  DialogTrigger,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { SearchRegular, AddRegular, EyeRegular } from '@fluentui/react-icons';

interface CannedScriptInfo {
  scriptId: string;
  label: string;
  phase: string;
  description: string;
  objectCount: number;
}

const AZURE_SERVICES = [
  {
    service: 'Azure SQL Database',
    tiers: [
      'Basic (5 DTUs)',
      'Standard S0 (10 DTUs)',
      'Standard S1 (20 DTUs)',
      'Standard S2 (50 DTUs)',
      'Standard S3 (100 DTUs)',
      'Premium P1 (125 DTUs)',
      'Premium P2 (250 DTUs)',
      'Premium P4 (500 DTUs)',
      'General Purpose (2 vCores)',
      'General Purpose (4 vCores)',
      'General Purpose (8 vCores)',
      'General Purpose (16 vCores)',
      'Business Critical (4 vCores)',
      'Business Critical (8 vCores)',
      'Business Critical (16 vCores)',
    ],
  },
  {
    service: 'Azure SQL Database Hyperscale',
    tiers: [
      'Hyperscale (2 vCores)',
      'Hyperscale (4 vCores)',
      'Hyperscale (8 vCores)',
      'Hyperscale (16 vCores)',
      'Hyperscale (24 vCores)',
    ],
  },
  {
    service: 'Azure SQL Managed Instance',
    tiers: [
      'General Purpose (4 vCores)',
      'General Purpose (8 vCores)',
      'General Purpose (16 vCores)',
      'General Purpose (24 vCores)',
      'Business Critical (4 vCores)',
      'Business Critical (8 vCores)',
      'Business Critical (16 vCores)',
    ],
  },
  {
    service: 'SQL Server on Azure VM',
    tiers: [
      'Standard_D2s_v5 (2 vCPU, 8 GB)',
      'Standard_D4s_v5 (4 vCPU, 16 GB)',
      'Standard_D8s_v5 (8 vCPU, 32 GB)',
      'Standard_D16s_v5 (16 vCPU, 64 GB)',
      'Standard_E4s_v5 (4 vCPU, 32 GB)',
      'Standard_E8s_v5 (8 vCPU, 64 GB)',
      'Standard_E16s_v5 (16 vCPU, 128 GB)',
    ],
  },
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
  objectList: {
    maxHeight: '240px',
    overflowY: 'auto',
    borderTopWidth: '1px',
    borderRightWidth: '1px',
    borderBottomWidth: '1px',
    borderLeftWidth: '1px',
    borderTopStyle: 'solid',
    borderRightStyle: 'solid',
    borderBottomStyle: 'solid',
    borderLeftStyle: 'solid',
    borderTopColor: tokens.colorNeutralStroke1,
    borderRightColor: tokens.colorNeutralStroke1,
    borderBottomColor: tokens.colorNeutralStroke1,
    borderLeftColor: tokens.colorNeutralStroke1,
    borderRadius: tokens.borderRadiusMedium,
    padding: tokens.spacingHorizontalS,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  objectItem: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
  },
  toolbar: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    marginBottom: tokens.spacingVerticalS,
    alignItems: 'center',
    flexWrap: 'wrap',
  },
  filterGroup: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'center',
    flex: 1,
    marginLeft: tokens.spacingHorizontalM,
  },
  scheduleInput: {
    marginLeft: tokens.spacingHorizontalS,
  },
  actionsRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    alignItems: 'center',
  },
  summaryRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    paddingTop: tokens.spacingVerticalXS,
    paddingBottom: tokens.spacingVerticalXS,
  },
  summaryLabel: {
    minWidth: '140px',
    fontWeight: tokens.fontWeightSemibold,
  },
  radioDescription: {
    display: 'block',
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase200,
  },
});

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

      {/* Strategy */}
      <Card className={styles.card}>
        <CardHeader header={<Text className={styles.cardTitle}>Migration Strategy</Text>} />
        <RadioGroup
          value={strategy}
          onChange={(_e, data) => setStrategy(data.value as MigrationStrategy)}
        >
          {(Object.keys(STRATEGY_INFO) as MigrationStrategy[]).map((key) => (
            <Radio
              key={key}
              value={key}
              label={
                <>
                  {STRATEGY_INFO[key].label}
                  <span className={styles.radioDescription}>{STRATEGY_INFO[key].description}</span>
                </>
              }
            />
          ))}
        </RadioGroup>
      </Card>

      {/* Object Selection */}
      <Card className={styles.card}>
        <CardHeader header={<Text className={styles.cardTitle}>Schema Objects</Text>} />
        <div className={styles.toolbar}>
          <Button appearance="subtle" size="small" onClick={selectAll}>
            {typeFilter || nameFilter ? 'Select filtered' : 'Select all'}
          </Button>
          <Button appearance="subtle" size="small" onClick={deselectAll}>
            {typeFilter || nameFilter ? 'Deselect filtered' : 'Deselect all'}
          </Button>
          <div className={styles.filterGroup}>
            <Select
              size="small"
              value={typeFilter}
              onChange={(_e, data) => setTypeFilter(data.value)}
              style={{ minWidth: '140px' }}
            >
              <option value="">All types</option>
              {objectTypes.map((t) => (
                <option key={t} value={t}>{t}</option>
              ))}
            </Select>
            <Input
              size="small"
              contentBefore={<SearchRegular />}
              placeholder="Search objects..."
              value={nameFilter}
              onChange={(_e, data) => setNameFilter(data.value)}
              style={{ flex: 1 }}
            />
          </div>
        </div>
        <div className={styles.objectList}>
          {filteredObjects.length === 0 && allObjects.length > 0 && (
            <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
              No objects match the current filters.
            </Text>
          )}
          {allObjects.length === 0 && (
            <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
              No assessment data available. Run an assessment first.
            </Text>
          )}
          {filteredObjects.map((obj) => {
            const key = objectKey(obj);
            return (
              <div key={key} className={styles.objectItem}>
                <Checkbox
                  checked={selectedObjects.has(key)}
                  onChange={() => toggleObject(key)}
                  label={
                    <span className={styles.objectItem}>
                      <Badge appearance="filled" color="informative" size="small">{obj.objectType}</Badge>
                      <Text size={200}>{key}</Text>
                    </span>
                  }
                />
              </div>
            );
          })}
        </div>
        <Text size={200} style={{ color: tokens.colorNeutralForeground3, marginTop: tokens.spacingVerticalS, display: 'block' }}>
          {typeFilter || nameFilter
            ? `${filteredObjects.filter((o) => selectedObjects.has(objectKey(o))).length} of ${filteredObjects.length} shown (${selectedObjects.size} of ${allObjects.length} total selected)`
            : `${selectedObjects.size} of ${allObjects.length} objects selected`}
        </Text>
      </Card>

      {/* Target Tier */}
      <Card className={styles.card}>
        <CardHeader header={<Text className={styles.cardTitle}>Target Tier</Text>} />
        {recommendation && (
          <MessageBar intent="info" style={{ marginBottom: tokens.spacingVerticalM }}>
            <MessageBarBody>
              Recommended: <strong>{recommendation.serviceTier}</strong> — {recommendation.computeSize}{' '}
              (~${regionPricing.length > 0 ? regionPricing[0].estimatedMonthlyCostUsd.toFixed(2) : recommendation.estimatedMonthlyCostUsd}/mo)
            </MessageBarBody>
          </MessageBar>
        )}
        <Field label="Target Service">
          <Select
            value={serviceOverride}
            onChange={(_e, data) => {
              setServiceOverride(data.value);
              setTierOverride('');
            }}
          >
            <option value="">Use recommended</option>
            {AZURE_SERVICES.map((s) => (
              <option key={s.service} value={s.service}>{s.service}</option>
            ))}
          </Select>
        </Field>
        {serviceOverride && (
          <Field label="Service Tier" style={{ marginTop: tokens.spacingVerticalS }}>
            <Select
              value={tierOverride}
              onChange={(_e, data) => setTierOverride(data.value)}
            >
              <option value="">Use recommended</option>
              {AZURE_SERVICES.find((s) => s.service === serviceOverride)?.tiers.map((t) => (
                <option key={t} value={t}>{t}</option>
              ))}
            </Select>
          </Field>
        )}
      </Card>

      {/* Deployment Region */}
      <Card className={styles.card}>
        <CardHeader header={<Text className={styles.cardTitle}>Deployment Region</Text>} />
        {loadingPricing && <Spinner label="Loading pricing…" />}
        {!loadingPricing && regionPricing.length === 0 && (
          <MessageBar intent="info">
            <MessageBarBody>No pricing data available</MessageBarBody>
          </MessageBar>
        )}
        {!loadingPricing && regionPricing.length > 0 && (
          <>
            <RadioGroup
              value={selectedRegion}
              onChange={(_e, data) => setSelectedRegion(data.value)}
            >
              <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <thead>
                  <tr style={{ textAlign: 'left', borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>
                    <th style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}` }}></th>
                    <th style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}` }}>Region</th>
                    <th style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}` }}>Region Code</th>
                    <th style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`, textAlign: 'right' }}>Est. Monthly Cost</th>
                  </tr>
                </thead>
                <tbody>
                  {regionPricing.slice(0, 15).map((r) => (
                    <tr key={r.armRegionName} style={{ borderBottom: `1px solid ${tokens.colorNeutralStroke1}` }}>
                      <td style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}` }}>
                        <Radio value={r.armRegionName} label="" />
                      </td>
                      <td style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}` }}>{r.displayName}</td>
                      <td style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}` }}>
                        <Text size={200} style={{ fontFamily: 'monospace' }}>{r.armRegionName}</Text>
                      </td>
                      <td style={{ padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`, textAlign: 'right' }}>
                        ${r.estimatedMonthlyCostUsd.toFixed(2)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </RadioGroup>
            {selectedRegion && (() => {
              const sel = regionPricing.find((r) => r.armRegionName === selectedRegion);
              return sel ? (
                <Text size={200} style={{ marginTop: tokens.spacingVerticalS, display: 'block' }}>
                  Selected: {sel.displayName} — ~${sel.estimatedMonthlyCostUsd.toFixed(2)}/mo
                </Text>
              ) : null;
            })()}
          </>
        )}
      </Card>

      {/* Target Database */}
      <Card className={styles.card}>
        <CardHeader header={<Text className={styles.cardTitle}>Target Database</Text>} />
        <Checkbox
          label="Use an existing Azure SQL database as the migration target"
          checked={useExistingTarget}
          onChange={(_, data) => setUseExistingTarget(data.checked === true)}
        />
        {useExistingTarget && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginTop: '12px' }}>
            <Field label="Server">
              <Input placeholder="myserver.database.windows.net" value={targetServer} onChange={(_, d) => setTargetServer(d.value)} />
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

      {/* Migration Scripts */}
      <Card className={styles.card}>
        <CardHeader header={<Text className={styles.cardTitle}>Pre-Migration Scripts</Text>} />
        {availableScripts.filter(s => s.phase === 'Pre').map(script => (
          <div key={script.scriptId} style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS, marginBottom: tokens.spacingVerticalXS }}>
            <Checkbox
              checked={selectedScripts.has(script.scriptId)}
              onChange={() => toggleScript(script.scriptId)}
              label={`${script.label} (${script.objectCount})`}
            />
            <Button
              appearance="subtle"
              size="small"
              icon={<EyeRegular />}
              onClick={() => previewCannedScript(script.scriptId)}
              title="Preview SQL"
            />
          </div>
        ))}
        {customScripts.filter(s => s.phase === 'Pre').map(script => (
          <div key={script.scriptId} style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS, marginBottom: tokens.spacingVerticalXS }}>
            <Badge appearance="outline">Custom</Badge>
            <Text>{script.label}</Text>
            <Button appearance="subtle" size="small" onClick={() => removeCustomScript(script.scriptId)}>✕</Button>
          </div>
        ))}
        <Button
          appearance="outline"
          size="small"
          icon={<AddRegular />}
          onClick={() => { setCustomDialogPhase('Pre'); setShowCustomDialog(true); }}
          style={{ marginTop: tokens.spacingVerticalS }}
        >
          Add Custom Script
        </Button>
      </Card>

      <Card className={styles.card}>
        <CardHeader header={<Text className={styles.cardTitle}>Post-Migration Scripts</Text>} />
        {availableScripts.filter(s => s.phase === 'Post').map(script => (
          <div key={script.scriptId} style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS, marginBottom: tokens.spacingVerticalXS }}>
            <Checkbox
              checked={selectedScripts.has(script.scriptId)}
              onChange={() => toggleScript(script.scriptId)}
              label={`${script.label} (${script.objectCount})`}
            />
            <Button
              appearance="subtle"
              size="small"
              icon={<EyeRegular />}
              onClick={() => previewCannedScript(script.scriptId)}
              title="Preview SQL"
            />
          </div>
        ))}
        {customScripts.filter(s => s.phase === 'Post').map(script => (
          <div key={script.scriptId} style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS, marginBottom: tokens.spacingVerticalXS }}>
            <Badge appearance="outline">Custom</Badge>
            <Text>{script.label}</Text>
            <Button appearance="subtle" size="small" onClick={() => removeCustomScript(script.scriptId)}>✕</Button>
          </div>
        ))}
        <Button
          appearance="outline"
          size="small"
          icon={<AddRegular />}
          onClick={() => { setCustomDialogPhase('Post'); setShowCustomDialog(true); }}
          style={{ marginTop: tokens.spacingVerticalS }}
        >
          Add Custom Script
        </Button>
      </Card>

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
        <Card className={styles.card}>
          <CardHeader header={<Text className={styles.cardTitle}>Review &amp; Approve</Text>} />
          <div>
            <div className={styles.summaryRow}>
              <Text className={styles.summaryLabel}>Strategy</Text>
              <Text>{STRATEGY_INFO[savedPlan.strategy].label}</Text>
            </div>
            <div className={styles.summaryRow}>
              <Text className={styles.summaryLabel}>Objects</Text>
              <Text>{savedPlan.includedObjects.length} included, {savedPlan.excludedObjects.length} excluded</Text>
            </div>
            <div className={styles.summaryRow}>
              <Text className={styles.summaryLabel}>Service</Text>
              <Text>{savedPlan.targetTier.serviceTier}</Text>
            </div>
            <div className={styles.summaryRow}>
              <Text className={styles.summaryLabel}>Tier</Text>
              <Text>{savedPlan.targetTier.computeSize}</Text>
            </div>
            <div className={styles.summaryRow}>
              <Text className={styles.summaryLabel}>Schedule</Text>
              <Text>{savedPlan.scheduledAt ? new Date(savedPlan.scheduledAt).toLocaleString() : 'Immediately on approval'}</Text>
            </div>
            {(selectedScripts.size > 0 || customScripts.length > 0) && (
              <>
                <div className={styles.summaryRow}>
                  <Text className={styles.summaryLabel}>Pre-migration</Text>
                  <Text>
                    {availableScripts.filter(s => s.phase === 'Pre' && selectedScripts.has(s.scriptId)).length} canned
                    {customScripts.filter(s => s.phase === 'Pre').length > 0 && `, ${customScripts.filter(s => s.phase === 'Pre').length} custom`}
                  </Text>
                </div>
                <div className={styles.summaryRow}>
                  <Text className={styles.summaryLabel}>Post-migration</Text>
                  <Text>
                    {availableScripts.filter(s => s.phase === 'Post' && selectedScripts.has(s.scriptId)).length} canned
                    {customScripts.filter(s => s.phase === 'Post').length > 0 && `, ${customScripts.filter(s => s.phase === 'Post').length} custom`}
                  </Text>
                </div>
              </>
            )}
            <div className={styles.summaryRow}>
              <Text className={styles.summaryLabel}>Status</Text>
              <Text>{savedPlan.isApproved ? `Approved by ${savedPlan.approvedBy}` : 'Pending approval'}</Text>
            </div>
          </div>
          <div className={styles.actionsRow} style={{ marginTop: tokens.spacingVerticalM }}>
            <Button
              appearance="primary"
              onClick={approvePlan}
              disabled={approving || savedPlan.isApproved}
            >
              {approving ? 'Approving…' : savedPlan.isApproved ? 'Approved ✓' : 'Approve'}
            </Button>
          </div>
        </Card>
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
