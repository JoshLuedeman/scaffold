import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Badge,
  Button,
  Card,
  Dialog,
  DialogActions,
  DialogBody,
  DialogSurface,
  DialogTitle,
  Input,
  Spinner,
  Text,
  Textarea,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import type { BadgeProps } from '@fluentui/react-components';
import {
  AddRegular,
  DeleteRegular,
  FolderOpenRegular,
} from '@fluentui/react-icons';
import { api } from '../services/api';
import type { MigrationProject, ProjectStatus } from '../types';

const statusColor: Record<ProjectStatus, BadgeProps['color']> = {
  Created: 'informative',
  Assessing: 'brand',
  Assessed: 'brand',
  PlanningMigration: 'warning',
  MigrationPlanned: 'warning',
  Migrating: 'warning',
  MigrationComplete: 'success',
  Failed: 'danger',
};

const useStyles = makeStyles({
  page: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))',
    gap: tokens.spacingHorizontalL,
  },
  card: {
    backgroundColor: tokens.colorNeutralCardBackground,
    padding: tokens.spacingVerticalL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    cursor: 'pointer',
    ':hover': {
      backgroundColor: tokens.colorNeutralCardBackgroundHover,
    },
  },
  cardHeader: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
  },
  cardTitle: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXXS,
    flex: 1,
    minWidth: 0,
  },
  cardMeta: {
    display: 'flex',
    gap: tokens.spacingHorizontalL,
    color: tokens.colorNeutralForeground3,
  },
  cardActions: {
    display: 'flex',
    justifyContent: 'flex-end',
    gap: tokens.spacingHorizontalS,
    marginTop: tokens.spacingVerticalXS,
  },
  subtitle: {
    color: tokens.colorNeutralForeground3,
  },
  centered: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    gap: tokens.spacingVerticalM,
    paddingTop: tokens.spacingVerticalXXXL,
    paddingBottom: tokens.spacingVerticalXXXL,
  },
  emptyIcon: {
    fontSize: '48px',
    color: tokens.colorNeutralForeground3,
  },
  form: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  dangerButton: {
    backgroundColor: tokens.colorPaletteRedBackground3,
    color: tokens.colorNeutralForegroundOnBrand,
    ':hover': {
      backgroundColor: tokens.colorPaletteRedForeground1,
      color: tokens.colorNeutralForegroundOnBrand,
    },
  },
});

export default function ProjectList() {
  const styles = useStyles();
  const navigate = useNavigate();

  const [projects, setProjects] = useState<MigrationProject[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [createOpen, setCreateOpen] = useState(false);
  const [newName, setNewName] = useState('');
  const [newDesc, setNewDesc] = useState('');
  const [saving, setSaving] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState<MigrationProject | null>(null);
  const [deleting, setDeleting] = useState(false);

  const loadProjects = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await api.get<MigrationProject[]>('/projects');
      setProjects(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load projects');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadProjects();
  }, [loadProjects]);

  const handleCreate = async () => {
    if (!newName.trim()) return;
    try {
      setSaving(true);
      await api.post('/projects', {
        name: newName.trim(),
        description: newDesc.trim() || undefined,
      });
      setCreateOpen(false);
      setNewName('');
      setNewDesc('');
      await loadProjects();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create project');
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      setDeleting(true);
      await api.delete(`/projects/${deleteTarget.id}`);
      setDeleteTarget(null);
      await loadProjects();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete project');
    } finally {
      setDeleting(false);
    }
  };

  if (loading) {
    return (
      <div className={styles.centered}>
        <Spinner label="Loading projects…" />
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.centered}>
        <Text weight="semibold">Error</Text>
        <Text>{error}</Text>
        <Button appearance="primary" onClick={loadProjects}>
          Retry
        </Button>
      </div>
    );
  }

  return (
    <div className={styles.page}>
      <div className={styles.header}>
        <Text as="h2" size={700} weight="semibold">
          Projects
        </Text>
        <Button
          appearance="primary"
          icon={<AddRegular />}
          onClick={() => setCreateOpen(true)}
        >
          New Project
        </Button>
      </div>

      {projects.length === 0 ? (
        <div className={styles.centered}>
          <FolderOpenRegular className={styles.emptyIcon} />
          <Text size={400}>No projects yet. Create one to get started.</Text>
          <Button
            appearance="primary"
            icon={<AddRegular />}
            onClick={() => setCreateOpen(true)}
          >
            New Project
          </Button>
        </div>
      ) : (
        <div className={styles.grid}>
          {projects.map((p) => (
            <Card key={p.id} className={styles.card} as="div" onClick={() => navigate(`/projects/${p.id}`)}>
              <div className={styles.cardHeader}>
                <div className={styles.cardTitle}>
                  <Text weight="semibold" size={400}>{p.name}</Text>
                  {p.description && (
                    <Text size={200} className={styles.subtitle} truncate wrap={false}>
                      {p.description}
                    </Text>
                  )}
                </div>
                <Badge appearance="filled" color={statusColor[p.status]}>
                  {p.status}
                </Badge>
              </div>
              <div className={styles.cardMeta}>
                <span>Created {new Date(p.createdAt).toLocaleDateString()}</span>
                <span>Updated {new Date(p.updatedAt).toLocaleDateString()}</span>
              </div>
              <div className={styles.cardActions}>
                <Button
                  appearance="outline"
                  size="small"
                  icon={<DeleteRegular />}
                  aria-label={`Delete ${p.name}`}
                  onClick={(e) => { e.stopPropagation(); setDeleteTarget(p); }}
                />
              </div>
            </Card>
          ))}
        </div>
      )}

      {/* Create Project Dialog */}
      <Dialog open={createOpen} onOpenChange={(_, data) => setCreateOpen(data.open)}>
        <DialogSurface>
          <DialogTitle>New Project</DialogTitle>
          <DialogBody>
            <div className={styles.form}>
              <Input
                placeholder="Project name"
                value={newName}
                onChange={(_, data) => setNewName(data.value)}
                required
              />
              <Textarea
                placeholder="Description (optional)"
                value={newDesc}
                onChange={(_, data) => setNewDesc(data.value)}
                resize="vertical"
              />
            </div>
          </DialogBody>
          <DialogActions>
            <Button appearance="secondary" onClick={() => setCreateOpen(false)}>
              Cancel
            </Button>
            <Button
              appearance="primary"
              onClick={handleCreate}
              disabled={!newName.trim() || saving}
              icon={saving ? <Spinner size="tiny" /> : undefined}
            >
              {saving ? 'Creating…' : 'Create'}
            </Button>
          </DialogActions>
        </DialogSurface>
      </Dialog>

      {/* Delete Confirmation Dialog */}
      <Dialog
        open={deleteTarget !== null}
        onOpenChange={(_, data) => {
          if (!data.open) setDeleteTarget(null);
        }}
      >
        <DialogSurface>
          <DialogTitle>Delete Project</DialogTitle>
          <DialogBody>
            <Text>
              Are you sure you want to delete{' '}
              <Text weight="semibold">{deleteTarget?.name}</Text>?
            </Text>
          </DialogBody>
          <DialogActions>
            <Button appearance="secondary" onClick={() => setDeleteTarget(null)}>
              Cancel
            </Button>
            <Button
              appearance="primary"
              className={styles.dangerButton}
              onClick={handleDelete}
              disabled={deleting}
              icon={deleting ? <Spinner size="tiny" /> : undefined}
            >
              {deleting ? 'Deleting…' : 'Delete'}
            </Button>
          </DialogActions>
        </DialogSurface>
      </Dialog>
    </div>
  );
}
