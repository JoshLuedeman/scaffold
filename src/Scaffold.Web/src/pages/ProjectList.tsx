import { useCallback, useEffect, useMemo, useState } from 'react';
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
  Select,
  Spinner,
  Text,
  Textarea,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import type { BadgeProps } from '@fluentui/react-components';
import {
  AddRegular,
  ArrowLeftRegular,
  ArrowRightRegular,
  DeleteRegular,
  FolderOpenRegular,
  SearchRegular,
} from '@fluentui/react-icons';
import { api } from '../services/api';
import type { MigrationProject, PaginatedResult, ProjectStatus } from '../types';

const ALL_STATUSES: ProjectStatus[] = [
  'Created',
  'Assessing',
  'Assessed',
  'PlanningMigration',
  'MigrationPlanned',
  'Migrating',
  'MigrationComplete',
  'Failed',
];

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
  toolbar: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: tokens.spacingHorizontalM,
    alignItems: 'center',
  },
  searchInput: {
    minWidth: '200px',
    flex: 1,
    maxWidth: '400px',
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
  pagination: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    gap: tokens.spacingHorizontalM,
    paddingTop: tokens.spacingVerticalM,
  },
});

export default function ProjectList() {
  const styles = useStyles();
  const navigate = useNavigate();

  const [projects, setProjects] = useState<MigrationProject[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Pagination state
  const [page, setPage] = useState(1);
  const [pageSize] = useState(25);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [hasNextPage, setHasNextPage] = useState(false);
  const [hasPreviousPage, setHasPreviousPage] = useState(false);

  // Search, filter, sort state
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('All');
  const [sortBy, setSortBy] = useState('name-asc');

  const [createOpen, setCreateOpen] = useState(false);
  const [newName, setNewName] = useState('');
  const [newDesc, setNewDesc] = useState('');
  const [saving, setSaving] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState<MigrationProject | null>(null);
  const [deleting, setDeleting] = useState(false);

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(searchQuery), 300);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  const loadProjects = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await api.get<PaginatedResult<MigrationProject>>(
        `/projects?page=${page}&pageSize=${pageSize}`,
      );
      setProjects(data.items);
      setTotalCount(data.totalCount);
      setTotalPages(data.totalPages);
      setHasNextPage(data.hasNextPage);
      setHasPreviousPage(data.hasPreviousPage);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load projects');
    } finally {
      setLoading(false);
    }
  }, [page, pageSize]);

  useEffect(() => {
    loadProjects();
  }, [loadProjects]);

  // Client-side filtering and sorting
  const filteredProjects = useMemo(() => {
    let result = [...projects];

    if (debouncedSearch) {
      const query = debouncedSearch.toLowerCase();
      result = result.filter((p) => p.name.toLowerCase().includes(query));
    }

    if (statusFilter !== 'All') {
      result = result.filter((p) => p.status === statusFilter);
    }

    result.sort((a, b) => {
      switch (sortBy) {
        case 'name-asc':
          return a.name.localeCompare(b.name);
        case 'name-desc':
          return b.name.localeCompare(a.name);
        case 'created-newest':
          return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
        case 'created-oldest':
          return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
        case 'updated-newest':
          return new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime();
        case 'updated-oldest':
          return new Date(a.updatedAt).getTime() - new Date(b.updatedAt).getTime();
        default:
          return 0;
      }
    });

    return result;
  }, [projects, debouncedSearch, statusFilter, sortBy]);

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

  const hasFiltersActive = debouncedSearch !== '' || statusFilter !== 'All';

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

      {/* Search, Filter, Sort toolbar */}
      <div className={styles.toolbar}>
        <Input
          className={styles.searchInput}
          contentBefore={<SearchRegular />}
          placeholder="Search projects…"
          value={searchQuery}
          onChange={(_, data) => setSearchQuery(data.value)}
          aria-label="Search projects"
        />
        <Select
          value={statusFilter}
          onChange={(_, data) => setStatusFilter(data.value)}
          aria-label="Filter by status"
        >
          <option value="All">All statuses</option>
          {ALL_STATUSES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </Select>
        <Select
          value={sortBy}
          onChange={(_, data) => setSortBy(data.value)}
          aria-label="Sort projects"
        >
          <option value="name-asc">Name A–Z</option>
          <option value="name-desc">Name Z–A</option>
          <option value="created-newest">Created newest</option>
          <option value="created-oldest">Created oldest</option>
          <option value="updated-newest">Updated newest</option>
          <option value="updated-oldest">Updated oldest</option>
        </Select>
      </div>

      {filteredProjects.length === 0 ? (
        <div className={styles.centered}>
          <FolderOpenRegular className={styles.emptyIcon} />
          {hasFiltersActive ? (
            <Text size={400}>No projects match your filters.</Text>
          ) : (
            <>
              <Text size={400}>No projects yet. Create one to get started.</Text>
              <Button
                appearance="primary"
                icon={<AddRegular />}
                onClick={() => setCreateOpen(true)}
              >
                New Project
              </Button>
            </>
          )}
        </div>
      ) : (
        <div className={styles.grid}>
          {filteredProjects.map((p) => (
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

      {/* Pagination Controls */}
      {totalPages > 1 && (
        <div className={styles.pagination}>
          <Button
            appearance="subtle"
            icon={<ArrowLeftRegular />}
            disabled={!hasPreviousPage}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            aria-label="Previous page"
          >
            Previous
          </Button>
          <Text>
            Page {page} of {totalPages} ({totalCount} total)
          </Text>
          <Button
            appearance="subtle"
            icon={<ArrowRightRegular />}
            iconPosition="after"
            disabled={!hasNextPage}
            onClick={() => setPage((p) => p + 1)}
            aria-label="Next page"
          >
            Next
          </Button>
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