import { useState, useEffect, useMemo, useCallback } from 'react';
import type { AssessmentReport as Report, RiskRating, CompatibilitySeverity, ServiceCompatibility, CompatibilityIssue, DatabasePlatform } from '../types';
import { api } from '../services/api';
import {
  Button,
  Card,
  CardHeader,
  Text,
  Badge,
  MessageBar,
  MessageBarBody,
  Table,
  TableHeader,
  TableRow,
  TableCell,
  TableBody,
  TableHeaderCell,
  makeStyles,
  tokens,
  Divider,
} from '@fluentui/react-components';
import {
  ChevronDownRegular,
  ChevronRightRegular,
  DatabaseRegular,
  TableRegular,
  GaugeRegular,
  ShieldCheckmarkRegular,
  ArrowTrendingRegular,
} from '@fluentui/react-icons';
import type { ReactNode } from 'react';

const riskColor: Record<RiskRating, 'success' | 'warning' | 'danger'> = {
  Low: 'success',
  Medium: 'warning',
  High: 'danger',
};

const severityColor: Record<CompatibilitySeverity, 'success' | 'warning' | 'danger'> = {
  Supported: 'success',
  Partial: 'warning',
  Unsupported: 'danger',
};

const severityLabel: Record<CompatibilitySeverity, string> = {
  Supported: 'Supported',
  Partial: 'Partial Support',
  Unsupported: 'Unsupported',
};

const severityOrder: Record<CompatibilitySeverity, number> = {
  Unsupported: 0,
  Partial: 1,
  Supported: 2,
};

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
    gap: tokens.spacingHorizontalM,
  },
  metricCard: {
    padding: tokens.spacingVerticalM,
  },
  metricIcon: {
    fontSize: '20px',
    color: tokens.colorBrandForeground1,
    marginBottom: tokens.spacingVerticalXS,
  },
  metricLabel: {
    color: tokens.colorNeutralForeground3,
    textTransform: 'uppercase',
    letterSpacing: '0.04em',
  },
  metricValue: {
    marginTop: tokens.spacingVerticalXS,
  },
  recommendationCard: {
    padding: tokens.spacingVerticalL,
    backgroundColor: tokens.colorNeutralBackground2,
  },
  recommendationGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))',
    gap: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalL}`,
    marginTop: tokens.spacingVerticalM,
  },
  detailLabel: {
    color: tokens.colorNeutralForeground3,
  },
  reasoning: {
    marginTop: tokens.spacingVerticalM,
    fontStyle: 'italic',
    color: tokens.colorNeutralForeground3,
  },
  noIssues: {
    padding: tokens.spacingVerticalM,
    backgroundColor: tokens.colorPaletteGreenBackground1,
    borderRadius: tokens.borderRadiusMedium,
    color: tokens.colorPaletteGreenForeground1,
  },
  docLink: {
    marginLeft: tokens.spacingHorizontalXS,
    fontSize: tokens.fontSizeBase200,
  },
  serviceTable: {
    marginBottom: tokens.spacingVerticalM,
  },
  serviceRow: {
    cursor: 'pointer',
    '&:hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  serviceRowSelected: {
    cursor: 'pointer',
    backgroundColor: tokens.colorBrandBackground2,
    '&:hover': {
      backgroundColor: tokens.colorBrandBackground2Hover,
    },
  },
  recommended: {
    fontSize: tokens.fontSizeBase200,
    marginLeft: tokens.spacingHorizontalXS,
  },
  issueExpandRow: {
    cursor: 'pointer',
    '&:hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  issueDetailCell: {
    backgroundColor: tokens.colorNeutralBackground2,
    padding: tokens.spacingVerticalM,
    paddingLeft: tokens.spacingHorizontalXL,
  },
  issueDetailGrid: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalL}`,
  },
  groupByControls: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    alignItems: 'center',
    marginBottom: tokens.spacingVerticalS,
  },
  severityBadges: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
    marginLeft: tokens.spacingHorizontalM,
  },
  groupHeader: {
    backgroundColor: tokens.colorNeutralBackground3,
    fontWeight: tokens.fontWeightSemibold,
  },
  strategyRecommendationCard: {
    padding: tokens.spacingVerticalL,
    backgroundColor: tokens.colorNeutralBackground2,
  },
  downtimeCards: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: tokens.spacingHorizontalM,
    marginTop: tokens.spacingVerticalM,
  },
  downtimeCard: {
    padding: tokens.spacingVerticalM,
    textAlign: 'center' as const,
  },
});

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

const SQL_SERVER_TIERS: Record<string, string> = {
  Basic: 'Basic',
  Standard: 'Standard',
  Premium: 'Premium',
  Hyperscale: 'Hyperscale',
};

const POSTGRESQL_TIERS: Record<string, string> = {
  Basic: 'Burstable',
  Standard: 'General Purpose',
  Premium: 'Memory Optimized',
  Burstable: 'Burstable',
  'General Purpose': 'General Purpose',
  'Memory Optimized': 'Memory Optimized',
  GeneralPurpose: 'General Purpose',
  MemoryOptimized: 'Memory Optimized',
};

function formatTierLabel(tier: string | undefined, isPostgreSql: boolean): string {
  if (!tier) return 'Not Available';
  if (isPostgreSql) return POSTGRESQL_TIERS[tier] ?? tier;
  return SQL_SERVER_TIERS[tier] ?? tier;
}

function MetricCard({ icon, label, value }: { icon: ReactNode; label: string; value: ReactNode }) {
  const styles = useStyles();
  return (
    <Card className={styles.metricCard}>
      <div className={styles.metricIcon}>{icon}</div>
      <Text size={200} weight="regular" className={styles.metricLabel}>{label}</Text>
      <Text size={500} weight="bold" className={styles.metricValue} block>{value}</Text>
    </Card>
  );
}

export type IssueGroupBy = 'none' | 'severity' | 'type';

export default function AssessmentReport({ report, projectId, platform }: { report: Report; projectId: string; platform?: DatabasePlatform }) {
  const styles = useStyles();
  const { schema, dataProfile, performance, compatibilityIssues, recommendation, compatibilityScore, risk, strategyRecommendation } = report;
  const isPostgreSql = platform === 'PostgreSql';
  const [serviceSummaries, setServiceSummaries] = useState<ServiceCompatibility[]>([]);
  const [selectedService, setSelectedService] = useState<string | null>(null);
  const [serviceFilteredIssues, setServiceFilteredIssues] = useState<CompatibilityIssue[]>([]);
  const [expandedIssues, setExpandedIssues] = useState<Set<number>>(new Set());
  const [groupBy, setGroupBy] = useState<IssueGroupBy>('none');

  useEffect(() => {
    api.get<ServiceCompatibility[]>(`/projects/${projectId}/assessments/compatibility-summary`)
      .then((data) => setServiceSummaries(data))
      .catch(() => setServiceSummaries([]));
  }, [projectId, report.id]);

  const defaultFilteredIssues = useMemo(
    () => [...compatibilityIssues]
      .filter((i) => i.severity !== 'Supported')
      .sort((a, b) => severityOrder[a.severity] - severityOrder[b.severity]),
    [compatibilityIssues],
  );

  useEffect(() => {
    if (!selectedService) return;
    api.post<{ compatibilityIssues: CompatibilityIssue[] }>(`/projects/${projectId}/assessments/evaluate-target`, { targetService: selectedService })
      .then((data) => {
        const issues = data.compatibilityIssues
          .filter((i) => i.severity !== 'Supported')
          .sort((a, b) => severityOrder[a.severity] - severityOrder[b.severity]);
        setServiceFilteredIssues(issues);
      })
      .catch(() => {});
  }, [selectedService, projectId]);

  const displayIssues = selectedService ? serviceFilteredIssues : defaultFilteredIssues;
  const unsupportedCount = displayIssues.filter((i) => i.severity === 'Unsupported').length;
  const partialCount = displayIssues.filter((i) => i.severity === 'Partial').length;

  const toggleIssueExpand = useCallback((index: number) => {
    setExpandedIssues((prev) => {
      const next = new Set(prev);
      if (next.has(index)) next.delete(index);
      else next.add(index);
      return next;
    });
  }, []);

  const groupedIssues = useMemo(() => {
    if (groupBy === 'none') return null;
    const groups: Record<string, CompatibilityIssue[]> = {};
    for (const issue of displayIssues) {
      const key = groupBy === 'severity' ? issue.severity : issue.issueType;
      if (!groups[key]) groups[key] = [];
      groups[key].push(issue);
    }
    // Sort groups: severity groups in severity order, type groups alphabetically
    const sortedKeys = Object.keys(groups).sort((a, b) => {
      if (groupBy === 'severity') {
        return (severityOrder[a as CompatibilitySeverity] ?? 99) - (severityOrder[b as CompatibilitySeverity] ?? 99);
      }
      return a.localeCompare(b);
    });
    return sortedKeys.map((key) => ({ key, issues: groups[key] }));
  }, [displayIssues, groupBy]);

  return (
    <div className={styles.root}>
      {/* Summary Metrics */}
      <div className={styles.grid}>
        <MetricCard icon={<TableRegular />} label="Tables" value={schema.tableCount} />
        <MetricCard icon={<TableRegular />} label="Views" value={schema.viewCount} />
        {(!isPostgreSql || schema.storedProcedureCount > 0) && (
          <MetricCard icon={<DatabaseRegular />} label="Stored Procs" value={schema.storedProcedureCount} />
        )}
        {isPostgreSql && schema.extensionCount != null && (
          <MetricCard icon={<DatabaseRegular />} label="Extensions" value={schema.extensionCount} />
        )}
        {isPostgreSql && schema.sequenceCount != null && (
          <MetricCard icon={<DatabaseRegular />} label="Sequences" value={schema.sequenceCount} />
        )}
        <MetricCard icon={<DatabaseRegular />} label="Total Rows" value={dataProfile.totalRowCount.toLocaleString()} />
        <MetricCard icon={<DatabaseRegular />} label="Total Size" value={formatBytes(dataProfile.totalSizeBytes)} />
        <MetricCard icon={<ShieldCheckmarkRegular />} label="Compatibility" value={`${compatibilityScore}%`} />
        <MetricCard
          icon={<ArrowTrendingRegular />}
          label="Risk"
          value={<Badge appearance="filled" color={riskColor[risk]}>{risk}</Badge>}
        />
      </div>

      {/* Performance Profile */}
      <Divider />
      <Text size={400} weight="semibold">Performance Profile</Text>
      <div className={styles.grid}>
        <MetricCard icon={<GaugeRegular />} label="Avg CPU" value={`${performance.avgCpuPercent.toFixed(1)}%`} />
        <MetricCard icon={<GaugeRegular />} label="Memory Used" value={`${(performance.memoryUsedMb / 1024).toFixed(2)} GB`} />
        <MetricCard icon={<GaugeRegular />} label="Avg I/O" value={`${performance.avgIoMbPerSecond.toFixed(1)} MB/s`} />
        <MetricCard icon={<GaugeRegular />} label="Max DB Size" value={`${performance.maxDatabaseSizeMb} MB`} />
      </div>

      {/* Tier Recommendation */}
      <Divider />
      <Text size={400} weight="semibold">Tier Recommendation</Text>
      <Card className={styles.recommendationCard}>
        <CardHeader
          header={<Text weight="semibold">{formatTierLabel(recommendation.serviceTier, isPostgreSql)} — {recommendation.computeSize || 'N/A'}</Text>}
        />
        <div className={styles.recommendationGrid}>
          {recommendation.vCores != null && (
            <div>
              <Text size={200} className={styles.detailLabel} block>vCores</Text>
              <Text weight="semibold">{recommendation.vCores}</Text>
            </div>
          )}
          {recommendation.dtus != null && (
            <div>
              <Text size={200} className={styles.detailLabel} block>DTUs</Text>
              <Text weight="semibold">{recommendation.dtus}</Text>
            </div>
          )}
          <div>
            <Text size={200} className={styles.detailLabel} block>Storage</Text>
            <Text weight="semibold">{recommendation.storageGb} GB</Text>
          </div>
          <div>
            <Text size={200} className={styles.detailLabel} block>Est. Monthly Cost</Text>
            <Text weight="semibold">${recommendation.estimatedMonthlyCostUsd.toFixed(2)}</Text>
          </div>
        </div>
        {recommendation.reasoning && (
          <Text size={300} className={styles.reasoning} block>{recommendation.reasoning}</Text>
        )}
      </Card>

      {/* Strategy Recommendation */}
      {strategyRecommendation && (
        <>
          <Divider />
          <Text size={400} weight="semibold">Strategy Recommendation</Text>
          <Card className={styles.strategyRecommendationCard}>
            <CardHeader
              header={
                <div style={{ display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS }}>
                  <Text weight="semibold">{strategyRecommendation.recommendedStrategy === 'ContinuousSync' ? 'Continuous Sync' : 'Cutover'}</Text>
                  <Badge appearance="filled" color="brand">Recommended</Badge>
                </div>
              }
            />
            <Text size={300} className={styles.reasoning} block>
              {strategyRecommendation.reasoning}
            </Text>
            <div className={styles.downtimeCards}>
              <Card className={styles.downtimeCard}>
                <Text size={200} className={styles.detailLabel} block>Cutover Downtime</Text>
                <Text size={400} weight="semibold">{strategyRecommendation.estimatedDowntimeCutover}</Text>
              </Card>
              {strategyRecommendation.estimatedDowntimeContinuousSync && (
                <Card className={styles.downtimeCard}>
                  <Text size={200} className={styles.detailLabel} block>Continuous Sync Downtime</Text>
                  <Text size={400} weight="semibold">{strategyRecommendation.estimatedDowntimeContinuousSync}</Text>
                </Card>
              )}
            </div>
            {strategyRecommendation.considerations.length > 0 && (
              <div style={{ marginTop: tokens.spacingVerticalM, display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS }}>
                {strategyRecommendation.considerations.map((consideration, idx) => (
                  <MessageBar key={idx} intent="warning">
                    <MessageBarBody>{consideration}</MessageBarBody>
                  </MessageBar>
                ))}
              </div>
            )}
          </Card>
        </>
      )}

      {/* Service Compatibility */}
      <Divider />
      <Text size={400} weight="semibold">{isPostgreSql ? 'Azure PostgreSQL Service Compatibility' : 'Service Compatibility'}</Text>
      {serviceSummaries.length > 0 && (
        <Table className={styles.serviceTable}>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Azure Service</TableHeaderCell>
              <TableHeaderCell>Compatibility</TableHeaderCell>
              <TableHeaderCell>Risk</TableHeaderCell>
              <TableHeaderCell>Unsupported</TableHeaderCell>
              <TableHeaderCell>Partial</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {serviceSummaries.map((s) => (
              <TableRow
                key={s.service}
                className={selectedService === s.service ? styles.serviceRowSelected : styles.serviceRow}
                onClick={() => setSelectedService(selectedService === s.service ? null : s.service)}
              >
                <TableCell>
                  <Text weight={selectedService === s.service ? 'bold' : 'regular'}>
                    {s.service}
                  </Text>
                  {s.service === recommendation.serviceTier && (
                    <Badge appearance="filled" color="brand" className={styles.recommended}>Recommended</Badge>
                  )}
                </TableCell>
                <TableCell>
                  <Text weight="semibold">{s.compatibilityScore}%</Text>
                </TableCell>
                <TableCell>
                  <Badge appearance="filled" color={riskColor[s.risk]}>{s.risk}</Badge>
                </TableCell>
                <TableCell>
                  {s.unsupportedCount > 0 ? (
                    <Badge appearance="filled" color="danger">{s.unsupportedCount}</Badge>
                  ) : (
                    <Text size={300}>0</Text>
                  )}
                </TableCell>
                <TableCell>
                  {s.partialCount > 0 ? (
                    <Badge appearance="filled" color="warning">{s.partialCount}</Badge>
                  ) : (
                    <Text size={300}>0</Text>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      {/* Compatibility Issues */}
      <Divider />
      <div style={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap' }}>
        <Text size={400} weight="semibold">
          Compatibility Issues
          {selectedService ? (
            <Text size={400} weight="semibold"> — {selectedService} ({displayIssues.length} issues)</Text>
          ) : (
            <Text size={400} weight="semibold"> ({compatibilityIssues.length})</Text>
          )}
        </Text>
        {displayIssues.length > 0 && (
          <span className={styles.severityBadges}>
            {unsupportedCount > 0 && (
              <Badge appearance="filled" color="danger">{unsupportedCount} Unsupported</Badge>
            )}
            {partialCount > 0 && (
              <Badge appearance="filled" color="warning">{partialCount} Partial</Badge>
            )}
          </span>
        )}
      </div>
      {displayIssues.length === 0 ? (
        <Text className={styles.noIssues}>
          {selectedService ? `No compatibility issues for ${selectedService}.` : 'No compatibility issues found.'}
        </Text>
      ) : (
        <>
          <div className={styles.groupByControls}>
            <Text size={200}>Group by:</Text>
            <Button
              size="small"
              appearance={groupBy === 'none' ? 'primary' : 'subtle'}
              onClick={() => setGroupBy('none')}
            >
              None
            </Button>
            <Button
              size="small"
              appearance={groupBy === 'severity' ? 'primary' : 'subtle'}
              onClick={() => setGroupBy('severity')}
            >
              Severity
            </Button>
            <Button
              size="small"
              appearance={groupBy === 'type' ? 'primary' : 'subtle'}
              onClick={() => setGroupBy('type')}
            >
              Type
            </Button>
          </div>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHeaderCell style={{ width: '32px' }} />
                <TableHeaderCell>Object</TableHeaderCell>
                <TableHeaderCell>Type</TableHeaderCell>
                <TableHeaderCell>Description</TableHeaderCell>
                <TableHeaderCell>Severity</TableHeaderCell>
              </TableRow>
            </TableHeader>
            <TableBody>
              {groupedIssues ? (
                groupedIssues.map((group) => (
                  <IssueGroup
                    key={group.key}
                    groupKey={group.key}
                    issues={group.issues}
                    groupBy={groupBy}
                    expandedIssues={expandedIssues}
                    onToggleExpand={toggleIssueExpand}
                    styles={styles}
                    displayIssues={displayIssues}
                  />
                ))
              ) : (
                displayIssues.map((issue, i) => (
                  <IssueRow
                    key={i}
                    issue={issue}
                    index={i}
                    expanded={expandedIssues.has(i)}
                    onToggle={() => toggleIssueExpand(i)}
                    styles={styles}
                  />
                ))
              )}
            </TableBody>
          </Table>
        </>
      )}
    </div>
  );
}

function IssueGroup({
  groupKey,
  issues,
  groupBy,
  expandedIssues,
  onToggleExpand,
  styles,
  displayIssues,
}: {
  groupKey: string;
  issues: CompatibilityIssue[];
  groupBy: IssueGroupBy;
  expandedIssues: Set<number>;
  onToggleExpand: (index: number) => void;
  styles: ReturnType<typeof useStyles>;
  displayIssues: CompatibilityIssue[];
}) {
  const label = groupBy === 'severity'
    ? severityLabel[groupKey as CompatibilitySeverity] ?? groupKey
    : groupKey;

  return (
    <>
      <TableRow className={styles.groupHeader}>
        <TableCell colSpan={5}>
          <Text weight="semibold">{label} ({issues.length})</Text>
        </TableCell>
      </TableRow>
      {issues.map((issue) => {
        const globalIndex = displayIssues.indexOf(issue);
        return (
          <IssueRow
            key={globalIndex}
            issue={issue}
            index={globalIndex}
            expanded={expandedIssues.has(globalIndex)}
            onToggle={() => onToggleExpand(globalIndex)}
            styles={styles}
          />
        );
      })}
    </>
  );
}

function IssueRow({
  issue,
  index,
  expanded,
  onToggle,
  styles,
}: {
  issue: CompatibilityIssue;
  index: number;
  expanded: boolean;
  onToggle: () => void;
  styles: ReturnType<typeof useStyles>;
}) {
  return (
    <>
      <TableRow
        key={`row-${index}`}
        className={styles.issueExpandRow}
        onClick={onToggle}
        aria-expanded={expanded}
        data-testid={`issue-row-${index}`}
      >
        <TableCell>
          {expanded ? <ChevronDownRegular /> : <ChevronRightRegular />}
        </TableCell>
        <TableCell>{issue.objectName}</TableCell>
        <TableCell>{issue.issueType}</TableCell>
        <TableCell>{issue.description}</TableCell>
        <TableCell>
          <Badge appearance="filled" color={severityColor[issue.severity]}>
            {severityLabel[issue.severity]}
          </Badge>
        </TableCell>
      </TableRow>
      {expanded && (
        <TableRow key={`detail-${index}`}>
          <TableCell colSpan={5} className={styles.issueDetailCell}>
            <div className={styles.issueDetailGrid}>
              <div>
                <Text size={200} weight="semibold" block>Full Description</Text>
                <Text size={300}>{issue.description}</Text>
              </div>
              <div>
                <Text size={200} weight="semibold" block>Impact</Text>
                <Badge appearance="filled" color={issue.isBlocking ? 'danger' : 'warning'}>
                  {issue.isBlocking ? 'Blocking' : 'Non-blocking'}
                </Badge>
              </div>
              {issue.docUrl && (
                <div>
                  <Text size={200} weight="semibold" block>Documentation</Text>
                  <a href={issue.docUrl} target="_blank" rel="noopener noreferrer">
                    View remediation guidance →
                  </a>
                </div>
              )}
            </div>
          </TableCell>
        </TableRow>
      )}
    </>
  );
}
