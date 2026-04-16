import { useState, useEffect, useMemo } from 'react';
import type { AssessmentReport as Report, RiskRating, CompatibilitySeverity, ServiceCompatibility, CompatibilityIssue } from '../types';
import { api } from '../services/api';
import {
  Card,
  CardHeader,
  Text,
  Badge,
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
});

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
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

export default function AssessmentReport({ report, projectId }: { report: Report; projectId: string }) {
  const styles = useStyles();
  const { schema, dataProfile, performance, compatibilityIssues, recommendation, compatibilityScore, risk } = report;
  const [serviceSummaries, setServiceSummaries] = useState<ServiceCompatibility[]>([]);
  const [selectedService, setSelectedService] = useState<string | null>(null);
  const [serviceFilteredIssues, setServiceFilteredIssues] = useState<CompatibilityIssue[]>([]);

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

  return (
    <div className={styles.root}>
      {/* Summary Metrics */}
      <div className={styles.grid}>
        <MetricCard icon={<TableRegular />} label="Tables" value={schema.tableCount} />
        <MetricCard icon={<TableRegular />} label="Views" value={schema.viewCount} />
        <MetricCard icon={<DatabaseRegular />} label="Stored Procs" value={schema.storedProcedureCount} />
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
          header={<Text weight="semibold">{recommendation.serviceTier || 'Not Available'} — {recommendation.computeSize || 'N/A'}</Text>}
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

      {/* Service Compatibility */}
      <Divider />
      <Text size={400} weight="semibold">Service Compatibility</Text>
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
      <Text size={400} weight="semibold">
        Compatibility Issues
        {selectedService ? (
          <Text size={400} weight="semibold"> — {selectedService} ({displayIssues.length} issues)</Text>
        ) : (
          <Text size={400} weight="semibold"> ({compatibilityIssues.length})</Text>
        )}
        {unsupportedCount > 0 && <Text size={400} weight="semibold" style={{ color: tokens.colorPaletteRedForeground1 }}> — {unsupportedCount} unsupported</Text>}
      </Text>
      {displayIssues.length === 0 ? (
        <Text className={styles.noIssues}>
          {selectedService ? `No compatibility issues for ${selectedService}.` : 'No compatibility issues found.'}
        </Text>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Object</TableHeaderCell>
              <TableHeaderCell>Type</TableHeaderCell>
              <TableHeaderCell>Description</TableHeaderCell>
              <TableHeaderCell>Severity</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {displayIssues.map((issue, i) => (
              <TableRow key={i}>
                <TableCell>{issue.objectName}</TableCell>
                <TableCell>{issue.issueType}</TableCell>
                <TableCell>
                  {issue.description}
                  {issue.docUrl && (
                    <a href={issue.docUrl} target="_blank" rel="noopener noreferrer" className={styles.docLink}>
                      Learn more
                    </a>
                  )}
                </TableCell>
                <TableCell>
                  <Badge appearance="filled" color={severityColor[issue.severity]}>
                    {severityLabel[issue.severity]}
                  </Badge>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
