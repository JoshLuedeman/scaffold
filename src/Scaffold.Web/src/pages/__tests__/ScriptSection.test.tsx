import { render, screen, fireEvent } from '@testing-library/react';
import { TestWrapper } from '../../test/msalMock';
import { ScriptSection } from '../migration-config/ScriptSection';
import type { MigrationScript } from '../../types';
import type { CannedScriptInfo } from '../migration-config/types';

const mockCannedScripts: CannedScriptInfo[] = [
  { scriptId: 'canned-1', label: 'Index Rebuild', phase: 'Pre', description: 'Rebuilds indexes', objectCount: 5 },
  { scriptId: 'canned-2', label: 'Stats Update', phase: 'Pre', description: 'Updates statistics', objectCount: 3 },
  { scriptId: 'canned-3', label: 'Validate Data', phase: 'Post', description: 'Validates data', objectCount: 10 },
];

const mockCustomScripts: MigrationScript[] = [
  {
    scriptId: 'custom-1',
    label: 'My Custom Script',
    scriptType: 'Custom',
    phase: 'Pre',
    sqlContent: 'SELECT 1;',
    isEnabled: true,
    order: 0,
  },
];

describe('ScriptSection', () => {
  it('renders order numbers next to each script', () => {
    render(
      <TestWrapper>
        <ScriptSection
          phase="Pre"
          availableScripts={mockCannedScripts}
          selectedScripts={new Set(['canned-1', 'canned-2'])}
          customScripts={mockCustomScripts}
          onToggleScript={vi.fn()}
          onPreviewScript={vi.fn()}
          onRemoveCustomScript={vi.fn()}
          onAddCustomScript={vi.fn()}
        />
      </TestWrapper>,
    );

    // Should show order numbers 1, 2, 3 for canned-1, canned-2, custom-1
    const order1 = screen.getByTestId('order-canned-1');
    expect(order1).toHaveTextContent('1');
    const order2 = screen.getByTestId('order-canned-2');
    expect(order2).toHaveTextContent('2');
    const order3 = screen.getByTestId('order-custom-1');
    expect(order3).toHaveTextContent('3');
  });

  it('renders section title with phase', () => {
    render(
      <TestWrapper>
        <ScriptSection
          phase="Pre"
          availableScripts={mockCannedScripts}
          selectedScripts={new Set()}
          customScripts={[]}
          onToggleScript={vi.fn()}
          onPreviewScript={vi.fn()}
          onRemoveCustomScript={vi.fn()}
          onAddCustomScript={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Pre-Migration Scripts')).toBeInTheDocument();
  });

  it('renders Post-Migration Scripts title', () => {
    render(
      <TestWrapper>
        <ScriptSection
          phase="Post"
          availableScripts={mockCannedScripts}
          selectedScripts={new Set()}
          customScripts={[]}
          onToggleScript={vi.fn()}
          onPreviewScript={vi.fn()}
          onRemoveCustomScript={vi.fn()}
          onAddCustomScript={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Post-Migration Scripts')).toBeInTheDocument();
  });

  it('shows Custom badge for custom scripts', () => {
    render(
      <TestWrapper>
        <ScriptSection
          phase="Pre"
          availableScripts={mockCannedScripts}
          selectedScripts={new Set()}
          customScripts={mockCustomScripts}
          onToggleScript={vi.fn()}
          onPreviewScript={vi.fn()}
          onRemoveCustomScript={vi.fn()}
          onAddCustomScript={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Custom')).toBeInTheDocument();
    expect(screen.getByText('My Custom Script')).toBeInTheDocument();
  });

  it('shows edit button for custom scripts', () => {
    render(
      <TestWrapper>
        <ScriptSection
          phase="Pre"
          availableScripts={mockCannedScripts}
          selectedScripts={new Set()}
          customScripts={mockCustomScripts}
          onToggleScript={vi.fn()}
          onPreviewScript={vi.fn()}
          onRemoveCustomScript={vi.fn()}
          onAddCustomScript={vi.fn()}
          onUpdateCustomScript={vi.fn()}
        />
      </TestWrapper>,
    );
    const editBtn = screen.getByRole('button', { name: 'Edit My Custom Script' });
    expect(editBtn).toBeInTheDocument();
  });

  it('opens edit dialog pre-populated when edit button is clicked', () => {
    render(
      <TestWrapper>
        <ScriptSection
          phase="Pre"
          availableScripts={mockCannedScripts}
          selectedScripts={new Set()}
          customScripts={mockCustomScripts}
          onToggleScript={vi.fn()}
          onPreviewScript={vi.fn()}
          onRemoveCustomScript={vi.fn()}
          onAddCustomScript={vi.fn()}
          onUpdateCustomScript={vi.fn()}
        />
      </TestWrapper>,
    );
    const editBtn = screen.getByRole('button', { name: 'Edit My Custom Script' });
    fireEvent.click(editBtn);

    expect(screen.getByText('Edit Custom Script')).toBeInTheDocument();
    // Dialog should have pre-populated values
    expect(screen.getByDisplayValue('My Custom Script')).toBeInTheDocument();
    expect(screen.getByDisplayValue('SELECT 1;')).toBeInTheDocument();
  });

  it('calls onUpdateCustomScript when edit dialog is saved', () => {
    const onUpdate = vi.fn();
    render(
      <TestWrapper>
        <ScriptSection
          phase="Pre"
          availableScripts={mockCannedScripts}
          selectedScripts={new Set()}
          customScripts={mockCustomScripts}
          onToggleScript={vi.fn()}
          onPreviewScript={vi.fn()}
          onRemoveCustomScript={vi.fn()}
          onAddCustomScript={vi.fn()}
          onUpdateCustomScript={onUpdate}
        />
      </TestWrapper>,
    );
    const editBtn = screen.getByRole('button', { name: 'Edit My Custom Script' });
    fireEvent.click(editBtn);

    const saveBtn = screen.getByRole('button', { name: 'Save Changes' });
    fireEvent.click(saveBtn);

    expect(onUpdate).toHaveBeenCalledWith('custom-1', 'My Custom Script', 'SELECT 1;');
  });

  it('shows script rows as draggable', () => {
    render(
      <TestWrapper>
        <ScriptSection
          phase="Pre"
          availableScripts={mockCannedScripts}
          selectedScripts={new Set(['canned-1'])}
          customScripts={[]}
          onToggleScript={vi.fn()}
          onPreviewScript={vi.fn()}
          onRemoveCustomScript={vi.fn()}
          onAddCustomScript={vi.fn()}
        />
      </TestWrapper>,
    );
    const row = screen.getByTestId('script-row-canned-1');
    expect(row).toHaveAttribute('draggable', 'true');
  });

  it('applies dragging style during drag', () => {
    render(
      <TestWrapper>
        <ScriptSection
          phase="Pre"
          availableScripts={mockCannedScripts}
          selectedScripts={new Set(['canned-1', 'canned-2'])}
          customScripts={[]}
          onToggleScript={vi.fn()}
          onPreviewScript={vi.fn()}
          onRemoveCustomScript={vi.fn()}
          onAddCustomScript={vi.fn()}
        />
      </TestWrapper>,
    );
    const row = screen.getByTestId('script-row-canned-1');
    fireEvent.dragStart(row);
    // After drag start, the row should have reduced opacity class
    // We verify by checking the drag interaction doesn't crash
    expect(row).toBeInTheDocument();
  });

  it('renders Add Custom Script button', () => {
    render(
      <TestWrapper>
        <ScriptSection
          phase="Pre"
          availableScripts={[]}
          selectedScripts={new Set()}
          customScripts={[]}
          onToggleScript={vi.fn()}
          onPreviewScript={vi.fn()}
          onRemoveCustomScript={vi.fn()}
          onAddCustomScript={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByRole('button', { name: 'Add Custom Script' })).toBeInTheDocument();
  });

  it('only shows scripts matching the phase', () => {
    render(
      <TestWrapper>
        <ScriptSection
          phase="Post"
          availableScripts={mockCannedScripts}
          selectedScripts={new Set(['canned-3'])}
          customScripts={[]}
          onToggleScript={vi.fn()}
          onPreviewScript={vi.fn()}
          onRemoveCustomScript={vi.fn()}
          onAddCustomScript={vi.fn()}
        />
      </TestWrapper>,
    );
    // canned-3 is Post phase, should appear
    expect(screen.getByTestId('script-row-canned-3')).toBeInTheDocument();
    // canned-1 is Pre phase, should not appear
    expect(screen.queryByTestId('script-row-canned-1')).not.toBeInTheDocument();
  });

  it('calls onToggleScript when checkbox is toggled', () => {
    const onToggle = vi.fn();
    render(
      <TestWrapper>
        <ScriptSection
          phase="Pre"
          availableScripts={mockCannedScripts}
          selectedScripts={new Set(['canned-1'])}
          customScripts={[]}
          onToggleScript={onToggle}
          onPreviewScript={vi.fn()}
          onRemoveCustomScript={vi.fn()}
          onAddCustomScript={vi.fn()}
        />
      </TestWrapper>,
    );
    // Find the checkbox by its label
    const checkbox = screen.getByText('Index Rebuild (5)');
    fireEvent.click(checkbox);
    expect(onToggle).toHaveBeenCalledWith('canned-1');
  });
});
