import { useState, useMemo } from 'react';
import {
  Badge,
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
  Text,
  Textarea,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { AddRegular, ArrowUpRegular, ArrowDownRegular, EditRegular, EyeRegular } from '@fluentui/react-icons';
import type { MigrationScript } from '../../types';
import type { CannedScriptInfo } from './types';

const useStyles = makeStyles({
  card: {
    padding: tokens.spacingHorizontalXL,
  },
  cardTitle: {
    fontWeight: tokens.fontWeightSemibold,
  },
  scriptRow: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    marginBottom: tokens.spacingVerticalXS,
    paddingTop: tokens.spacingVerticalXS,
    paddingBottom: tokens.spacingVerticalXS,
    paddingLeft: tokens.spacingHorizontalS,
    paddingRight: tokens.spacingHorizontalS,
    borderRadius: tokens.borderRadiusMedium,
    cursor: 'grab',
    transitionProperty: 'opacity, background-color',
    transitionDuration: '0.2s',
  },
  dragging: {
    opacity: '0.4',
  },
  dragOver: {
    backgroundColor: tokens.colorBrandBackground2,
  },
  orderNumber: {
    minWidth: '24px',
    textAlign: 'center',
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase200,
  },
  dragHandle: {
    color: tokens.colorNeutralForeground3,
    cursor: 'grab',
    userSelect: 'none',
    fontSize: '16px',
    lineHeight: '1',
  },
});

interface ScriptItem {
  id: string;
  label: string;
  type: 'canned' | 'custom';
  objectCount?: number;
  script?: MigrationScript;
}

interface ScriptSectionProps {
  phase: 'Pre' | 'Post';
  availableScripts: CannedScriptInfo[];
  selectedScripts: Set<string>;
  customScripts: MigrationScript[];
  onToggleScript: (scriptId: string) => void;
  onPreviewScript: (scriptId: string) => void;
  onRemoveCustomScript: (scriptId: string) => void;
  onAddCustomScript: (phase: 'Pre' | 'Post') => void;
  onUpdateCustomScript?: (scriptId: string, label: string, content: string) => void;
}

export function ScriptSection({
  phase,
  availableScripts,
  selectedScripts,
  customScripts,
  onToggleScript,
  onPreviewScript,
  onRemoveCustomScript,
  onAddCustomScript,
  onUpdateCustomScript,
}: ScriptSectionProps) {
  const styles = useStyles();

  const phaseScripts = useMemo(
    () => availableScripts.filter(s => s.phase === phase),
    [availableScripts, phase],
  );

  const phaseCustomScripts = useMemo(
    () => customScripts.filter(s => s.phase === phase),
    [customScripts, phase],
  );

  // Build items map
  const itemsMap = useMemo(() => {
    const map = new Map<string, ScriptItem>();
    for (const s of phaseScripts) {
      map.set(s.scriptId, {
        id: s.scriptId,
        label: `${s.label} (${s.objectCount})`,
        type: 'canned',
        objectCount: s.objectCount,
      });
    }
    for (const s of phaseCustomScripts) {
      map.set(s.scriptId, {
        id: s.scriptId,
        label: s.label,
        type: 'custom',
        script: s,
      });
    }
    return map;
  }, [phaseScripts, phaseCustomScripts]);

  // Default order: canned first, then custom
  const defaultOrder = useMemo(
    () => [...phaseScripts.map(s => s.scriptId), ...phaseCustomScripts.map(s => s.scriptId)],
    [phaseScripts, phaseCustomScripts],
  );

  // Local order state for drag-and-drop
  const [localOrder, setLocalOrder] = useState<string[]>([]);

  // Reconciled order: keep existing order for known items, append new items
  const orderedItems = useMemo(() => {
    const existingSet = new Set(localOrder);
    const validLocal = localOrder.filter(id => itemsMap.has(id));
    const newItems = defaultOrder.filter(id => !existingSet.has(id));
    return [...validLocal, ...newItems]
      .map(id => itemsMap.get(id))
      .filter((item): item is ScriptItem => item !== undefined);
  }, [localOrder, defaultOrder, itemsMap]);

  // Drag state
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);

  // Edit dialog state
  const [editDialogOpen, setEditDialogOpen] = useState(false);
  const [editLabel, setEditLabel] = useState('');
  const [editContent, setEditContent] = useState('');
  const [editScriptId, setEditScriptId] = useState<string | null>(null);

  function handleDragStart(e: React.DragEvent, index: number) {
    setDraggedIndex(index);
    if (e.dataTransfer) {
      e.dataTransfer.effectAllowed = 'move';
    }
  }

  function handleDragOver(e: React.DragEvent, index: number) {
    e.preventDefault();
    if (e.dataTransfer) {
      e.dataTransfer.dropEffect = 'move';
    }
    setDragOverIndex(index);
  }

  function handleDragLeave() {
    setDragOverIndex(null);
  }

  function handleDrop(targetIndex: number) {
    if (draggedIndex === null || draggedIndex === targetIndex) {
      setDraggedIndex(null);
      setDragOverIndex(null);
      return;
    }
    const currentIds = orderedItems.map(item => item.id);
    const newIds = [...currentIds];
    const [moved] = newIds.splice(draggedIndex, 1);
    newIds.splice(targetIndex, 0, moved);
    setLocalOrder(newIds);
    setDraggedIndex(null);
    setDragOverIndex(null);
  }

  function handleDragEnd() {
    setDraggedIndex(null);
    setDragOverIndex(null);
  }

  function moveItem(index: number, direction: 'up' | 'down') {
    const targetIndex = direction === 'up' ? index - 1 : index + 1;
    if (targetIndex < 0 || targetIndex >= orderedItems.length) return;
    const currentIds = orderedItems.map(item => item.id);
    const newIds = [...currentIds];
    const [moved] = newIds.splice(index, 1);
    newIds.splice(targetIndex, 0, moved);
    setLocalOrder(newIds);
  }

  function openEditDialog(script: MigrationScript) {
    setEditScriptId(script.scriptId);
    setEditLabel(script.label);
    setEditContent(script.sqlContent);
    setEditDialogOpen(true);
  }

  function saveEdit() {
    if (!editScriptId || !editLabel.trim() || !editContent.trim()) return;
    onUpdateCustomScript?.(editScriptId, editLabel.trim(), editContent.trim());
    setEditDialogOpen(false);
    setEditScriptId(null);
  }

  return (
    <Card className={styles.card}>
      <CardHeader header={<Text className={styles.cardTitle}>{phase}-Migration Scripts</Text>} />
      {orderedItems.map((item, index) => {
        const rowClass = [
          styles.scriptRow,
          draggedIndex === index ? styles.dragging : '',
          dragOverIndex === index ? styles.dragOver : '',
        ].filter(Boolean).join(' ');

        return (
          <div
            key={item.id}
            className={rowClass}
            draggable
            onDragStart={(e) => handleDragStart(e, index)}
            onDragOver={(e) => handleDragOver(e, index)}
            onDragLeave={handleDragLeave}
            onDrop={() => handleDrop(index)}
            onDragEnd={handleDragEnd}
            data-testid={`script-row-${item.id}`}
          >
            <span className={styles.orderNumber} data-testid={`order-${item.id}`}>{index + 1}</span>
            <span className={styles.dragHandle} aria-hidden="true">{'\u28FF'}</span>
            <Button
              appearance="subtle"
              size="small"
              icon={<ArrowUpRegular />}
              aria-label={`Move ${item.label} up`}
              disabled={index === 0}
              onClick={() => moveItem(index, 'up')}
            />
            <Button
              appearance="subtle"
              size="small"
              icon={<ArrowDownRegular />}
              aria-label={`Move ${item.label} down`}
              disabled={index === orderedItems.length - 1}
              onClick={() => moveItem(index, 'down')}
            />
            {item.type === 'canned' ? (
              <>
                <Checkbox
                  checked={selectedScripts.has(item.id)}
                  onChange={() => onToggleScript(item.id)}
                  label={item.label}
                />
                <Button
                  appearance="subtle"
                  size="small"
                  icon={<EyeRegular />}
                  onClick={() => onPreviewScript(item.id)}
                  aria-label={`Preview ${item.label}`}
                />
              </>
            ) : (
              <>
                <Badge appearance="outline">Custom</Badge>
                <Text>{item.label}</Text>
                {item.script && (
                  <Button
                    appearance="subtle"
                    size="small"
                    icon={<EditRegular />}
                    onClick={() => openEditDialog(item.script!)}
                    aria-label={`Edit ${item.label}`}
                  />
                )}
                <Button
                  appearance="subtle"
                  size="small"
                  onClick={() => onRemoveCustomScript(item.id)}
                  aria-label={`Remove ${item.label}`}
                >
                  {'\u2715'}
                </Button>
              </>
            )}
          </div>
        );
      })}
      <Button
        appearance="outline"
        size="small"
        icon={<AddRegular />}
        onClick={() => onAddCustomScript(phase)}
        style={{ marginTop: tokens.spacingVerticalS }}
      >
        Add Custom Script
      </Button>

      {/* Edit Custom Script Dialog */}
      <Dialog open={editDialogOpen} onOpenChange={(_e, data) => setEditDialogOpen(data.open)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>Edit Custom Script</DialogTitle>
            <DialogContent>
              <Field label="Script Name" required style={{ marginBottom: tokens.spacingVerticalM }}>
                <Input
                  value={editLabel}
                  onChange={(_e, data) => setEditLabel(data.value)}
                />
              </Field>
              <Field label="SQL Content" required>
                <Textarea
                  value={editContent}
                  onChange={(_e, data) => setEditContent(data.value)}
                  resize="vertical"
                  style={{ minHeight: '200px', fontFamily: 'monospace' }}
                />
              </Field>
            </DialogContent>
            <DialogActions>
              <DialogTrigger disableButtonEnhancement>
                <Button appearance="secondary">Cancel</Button>
              </DialogTrigger>
              <Button
                appearance="primary"
                onClick={saveEdit}
                disabled={!editLabel.trim() || !editContent.trim()}
              >
                Save Changes
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </Card>
  );
}