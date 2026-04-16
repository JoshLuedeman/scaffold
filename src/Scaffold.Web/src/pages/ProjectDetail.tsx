import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbDivider,
  BreadcrumbButton,
  Text,
  Badge,
  TabList,
  Tab,
  Card,
  CardHeader,
  Button,
  Spinner,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import type { SelectTabData, SelectTabEventHandler } from '@fluentui/react-components';
import { api } from '../services/api';
import type { MigrationProject, ProjectStatus } from '../types';
import AssessmentReport from '../components/AssessmentReport';

type Phase = 'assess' | 'plan' | 'execute';

const statusColor: Record<ProjectStatus, 'success' | 'warning' | 'danger' | 'informative' | 'important'> = {
  Created: 'informative',
  Assessing: 'warning',
  Assessed: 'success',
  PlanningMigration: 'warning',
  MigrationPlanned: 'success',
  Migrating: 'important',
  MigrationComplete: 'success',
  Failed: 'danger',
};

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
  },
  description: {
    color: tokens.colorNeutralForeground3,
  },
  tabContent: {
    marginTop: tokens.spacingVerticalL,
  },
  ctaCard: {
    maxWidth: '480px',
  },
  ctaBody: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    padding: tokens.spacingVerticalM,
  },
  planGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
    gap: tokens.spacingHorizontalM,
  },
  planCardBody: {
    padding: tokens.spacingVerticalS,
    paddingLeft: tokens.spacingHorizontalM,
    paddingRight: tokens.spacingHorizontalM,
    paddingBottom: tokens.spacingVerticalM,
  },
  center: {
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    minHeight: '200px',
  },
  error: {
    color: tokens.colorPaletteRedForeground1,
  },
});

export default function ProjectDetail() {
  const { id } = useParams<{ id: string }>();
  const styles = useStyles();

  const [project, setProject] = useState<MigrationProject | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<Phase>('assess');

  useEffect(() => {
    if (!id) return;
    const fetchProject = async () => {
      setLoading(true);
      try {
        const data = await api.get<MigrationProject>(`/projects/${id}`);
        setProject(data);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load project');
      } finally {
        setLoading(false);
      }
    };
    fetchProject();
  }, [id]);

  const onTabSelect: SelectTabEventHandler = (_ev, data: SelectTabData) => {
    setActiveTab(data.value as Phase);
  };

  if (loading) {
    return (
      <div className={styles.center}>
        <Spinner label="Loading project…" />
      </div>
    );
  }

  if (error || !project) {
    return (
      <div className={styles.center}>
        <Text className={styles.error}>{error ?? 'Project not found'}</Text>
      </div>
    );
  }

  const { assessment, migrationPlan } = project;

  return (
    <div className={styles.root}>
      {/* Breadcrumb */}
      <Breadcrumb>
        <BreadcrumbItem>
          <BreadcrumbButton as="a" href="/">Projects</BreadcrumbButton>
        </BreadcrumbItem>
        <BreadcrumbDivider />
        <BreadcrumbItem>
          <BreadcrumbButton current>{project.name}</BreadcrumbButton>
        </BreadcrumbItem>
      </Breadcrumb>

      {/* Title + Status */}
      <div className={styles.header}>
        <Text size={700} weight="bold">{project.name}</Text>
        <Badge appearance="filled" color={statusColor[project.status]}>{project.status}</Badge>
      </div>

      {project.description && (
        <Text size={300} className={styles.description}>{project.description}</Text>
      )}

      {/* Workflow Tabs */}
      <TabList selectedValue={activeTab} onTabSelect={onTabSelect}>
        <Tab value="assess">Assess</Tab>
        <Tab value="plan">Plan</Tab>
        <Tab value="execute">Execute</Tab>
      </TabList>

      <div className={styles.tabContent}>
        {/* ---- Assess Tab ---- */}
        {activeTab === 'assess' && (
          <>
            {!assessment ? (
              <Card className={styles.ctaCard}>
                <div className={styles.ctaBody}>
                  <Text size={400} weight="semibold">Assessment</Text>
                  <Text size={300}>Run an assessment to analyze your source database.</Text>
                  <Button appearance="primary" as="a" href={`/projects/${id}/assess`}>
                    Start Assessment →
                  </Button>
                </div>
              </Card>
            ) : (
              <div>
                <AssessmentReport report={assessment} projectId={id!} />
                <Button
                  appearance="secondary"
                  as="a"
                  href={`/projects/${id}/assess`}
                  style={{ marginTop: tokens.spacingVerticalM }}
                >
                  Re-run Assessment
                </Button>
              </div>
            )}
          </>
        )}

        {/* ---- Plan Tab ---- */}
        {activeTab === 'plan' && (
          <>
            {!assessment ? (
              <Card className={styles.ctaCard}>
                <div className={styles.ctaBody}>
                  <Text size={400} weight="semibold">No Assessment</Text>
                  <Text size={300}>Complete an assessment first before creating a migration plan.</Text>
                </div>
              </Card>
            ) : !migrationPlan ? (
              <Card className={styles.ctaCard}>
                <div className={styles.ctaBody}>
                  <Text size={400} weight="semibold">Migration Plan</Text>
                  <Text size={300}>Create a migration plan based on the assessment results.</Text>
                  <Button appearance="primary" as="a" href={`/projects/${id}/configure`}>
                    Configure Migration →
                  </Button>
                </div>
              </Card>
            ) : (
              <div>
                <div className={styles.planGrid}>
                  <Card>
                    <CardHeader header={<Text weight="semibold">Strategy</Text>} />
                    <div className={styles.planCardBody}>
                      <Text>{migrationPlan.strategy}</Text>
                    </div>
                  </Card>
                  <Card>
                    <CardHeader header={<Text weight="semibold">Objects</Text>} />
                    <div className={styles.planCardBody}>
                      <Text>
                        {migrationPlan.includedObjects.length} included, {migrationPlan.excludedObjects.length} excluded
                      </Text>
                    </div>
                  </Card>
                  <Card>
                    <CardHeader header={<Text weight="semibold">Target Tier</Text>} />
                    <div className={styles.planCardBody}>
                      <Text>{migrationPlan.targetTier.serviceTier} — {migrationPlan.targetTier.computeSize}</Text>
                    </div>
                  </Card>
                  <Card>
                    <CardHeader header={<Text weight="semibold">Schedule</Text>} />
                    <div className={styles.planCardBody}>
                      <Text>
                        {migrationPlan.scheduledAt
                          ? new Date(migrationPlan.scheduledAt).toLocaleString()
                          : 'Not scheduled'}
                      </Text>
                    </div>
                  </Card>
                  <Card>
                    <CardHeader header={<Text weight="semibold">Approval</Text>} />
                    <div className={styles.planCardBody}>
                      {migrationPlan.isApproved ? (
                        <Badge appearance="filled" color="success">
                          Approved{migrationPlan.approvedBy ? ` by ${migrationPlan.approvedBy}` : ''}
                        </Badge>
                      ) : (
                        <Badge appearance="filled" color="warning">Pending Approval</Badge>
                      )}
                    </div>
                  </Card>
                </div>
                {!migrationPlan.isApproved && (
                  <Button
                    appearance="secondary"
                    as="a"
                    href={`/projects/${id}/configure`}
                    style={{ marginTop: tokens.spacingVerticalM }}
                  >
                    Edit Plan
                  </Button>
                )}
              </div>
            )}
          </>
        )}

        {/* ---- Execute Tab ---- */}
        {activeTab === 'execute' && (
          <>
            {!migrationPlan?.isApproved ? (
              <Card className={styles.ctaCard}>
                <div className={styles.ctaBody}>
                  <Text size={400} weight="semibold">Not Ready</Text>
                  <Text size={300}>Approve a migration plan first before executing.</Text>
                </div>
              </Card>
            ) : (
              <Card className={styles.ctaCard}>
                <div className={styles.ctaBody}>
                  <Text size={400} weight="semibold">Execute Migration</Text>
                  <Text size={300}>Your plan is approved. Start the migration when ready.</Text>
                  <Button appearance="primary" as="a" href={`/projects/${id}/execute`}>
                    Start Migration →
                  </Button>
                </div>
              </Card>
            )}
          </>
        )}
      </div>
    </div>
  );
}
