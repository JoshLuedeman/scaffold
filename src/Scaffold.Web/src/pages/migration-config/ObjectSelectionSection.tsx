import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  Input,
  Select,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { SearchRegular } from '@fluentui/react-icons';
import type { SchemaObject } from '../../types';
import { objectKey } from './types';

const useStyles = makeStyles({
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
});

interface ObjectSelectionSectionProps {
  allObjects: SchemaObject[];
  filteredObjects: SchemaObject[];
  selectedObjects: Set<string>;
  objectTypes: string[];
  typeFilter: string;
  nameFilter: string;
  onTypeFilterChange: (value: string) => void;
  onNameFilterChange: (value: string) => void;
  onToggleObject: (key: string) => void;
  onSelectAll: () => void;
  onDeselectAll: () => void;
}

export function ObjectSelectionSection({
  allObjects,
  filteredObjects,
  selectedObjects,
  objectTypes,
  typeFilter,
  nameFilter,
  onTypeFilterChange,
  onNameFilterChange,
  onToggleObject,
  onSelectAll,
  onDeselectAll,
}: ObjectSelectionSectionProps) {
  const styles = useStyles();

  return (
    <Card className={styles.card}>
      <CardHeader header={<Text className={styles.cardTitle}>Schema Objects</Text>} />
      <div className={styles.toolbar}>
        <Button appearance="subtle" size="small" onClick={onSelectAll}>
          {typeFilter || nameFilter ? 'Select filtered' : 'Select all'}
        </Button>
        <Button appearance="subtle" size="small" onClick={onDeselectAll}>
          {typeFilter || nameFilter ? 'Deselect filtered' : 'Deselect all'}
        </Button>
        <div className={styles.filterGroup}>
          <Select
            size="small"
            value={typeFilter}
            onChange={(_e, data) => onTypeFilterChange(data.value)}
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
            onChange={(_e, data) => onNameFilterChange(data.value)}
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
                onChange={() => onToggleObject(key)}
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
  );
}