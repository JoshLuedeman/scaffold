import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TestWrapper } from '../../../test/msalMock';
import { ObjectSelectionSection } from '../ObjectSelectionSection';
import type { SchemaObject } from '../../../types';

const mockObjects: SchemaObject[] = [
  { name: 'Users', schema: 'dbo', objectType: 'Table' },
  { name: 'Orders', schema: 'dbo', objectType: 'Table' },
  { name: 'vw_Active', schema: 'dbo', objectType: 'View' },
  { name: 'sp_GetUsers', schema: 'dbo', objectType: 'StoredProcedure' },
];

describe('ObjectSelectionSection', () => {
  it('renders Schema Objects card header', () => {
    render(
      <TestWrapper>
        <ObjectSelectionSection
          allObjects={mockObjects}
          filteredObjects={mockObjects}
          selectedObjects={new Set(['dbo.Users', 'dbo.Orders'])}
          objectTypes={['Table', 'View', 'StoredProcedure']}
          typeFilter=""
          nameFilter=""
          onTypeFilterChange={vi.fn()}
          onNameFilterChange={vi.fn()}
          onToggleObject={vi.fn()}
          onSelectAll={vi.fn()}
          onDeselectAll={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('Schema Objects')).toBeInTheDocument();
  });

  it('displays all filtered objects', () => {
    render(
      <TestWrapper>
        <ObjectSelectionSection
          allObjects={mockObjects}
          filteredObjects={mockObjects}
          selectedObjects={new Set()}
          objectTypes={['Table', 'View', 'StoredProcedure']}
          typeFilter=""
          nameFilter=""
          onTypeFilterChange={vi.fn()}
          onNameFilterChange={vi.fn()}
          onToggleObject={vi.fn()}
          onSelectAll={vi.fn()}
          onDeselectAll={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('dbo.Users')).toBeInTheDocument();
    expect(screen.getByText('dbo.Orders')).toBeInTheDocument();
    expect(screen.getByText('dbo.vw_Active')).toBeInTheDocument();
    expect(screen.getByText('dbo.sp_GetUsers')).toBeInTheDocument();
  });

  it('shows object type badges', () => {
    render(
      <TestWrapper>
        <ObjectSelectionSection
          allObjects={mockObjects}
          filteredObjects={mockObjects}
          selectedObjects={new Set()}
          objectTypes={['Table', 'View', 'StoredProcedure']}
          typeFilter=""
          nameFilter=""
          onTypeFilterChange={vi.fn()}
          onNameFilterChange={vi.fn()}
          onToggleObject={vi.fn()}
          onSelectAll={vi.fn()}
          onDeselectAll={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getAllByText('Table').length).toBeGreaterThanOrEqual(2);
    expect(screen.getAllByText('View').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('StoredProcedure').length).toBeGreaterThanOrEqual(1);
  });

  it('shows selection count summary', () => {
    render(
      <TestWrapper>
        <ObjectSelectionSection
          allObjects={mockObjects}
          filteredObjects={mockObjects}
          selectedObjects={new Set(['dbo.Users', 'dbo.Orders'])}
          objectTypes={['Table', 'View', 'StoredProcedure']}
          typeFilter=""
          nameFilter=""
          onTypeFilterChange={vi.fn()}
          onNameFilterChange={vi.fn()}
          onToggleObject={vi.fn()}
          onSelectAll={vi.fn()}
          onDeselectAll={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByText(/2 of 4 objects selected/)).toBeInTheDocument();
  });

  it('calls onSelectAll when Select all button is clicked', async () => {
    const onSelectAll = vi.fn();
    const user = userEvent.setup();
    render(
      <TestWrapper>
        <ObjectSelectionSection
          allObjects={mockObjects}
          filteredObjects={mockObjects}
          selectedObjects={new Set()}
          objectTypes={['Table', 'View', 'StoredProcedure']}
          typeFilter=""
          nameFilter=""
          onTypeFilterChange={vi.fn()}
          onNameFilterChange={vi.fn()}
          onToggleObject={vi.fn()}
          onSelectAll={onSelectAll}
          onDeselectAll={vi.fn()}
        />
      </TestWrapper>,
    );

    await user.click(screen.getByRole('button', { name: 'Select all' }));
    expect(onSelectAll).toHaveBeenCalledTimes(1);
  });

  it('calls onDeselectAll when Deselect all button is clicked', async () => {
    const onDeselectAll = vi.fn();
    const user = userEvent.setup();
    render(
      <TestWrapper>
        <ObjectSelectionSection
          allObjects={mockObjects}
          filteredObjects={mockObjects}
          selectedObjects={new Set(['dbo.Users'])}
          objectTypes={['Table', 'View', 'StoredProcedure']}
          typeFilter=""
          nameFilter=""
          onTypeFilterChange={vi.fn()}
          onNameFilterChange={vi.fn()}
          onToggleObject={vi.fn()}
          onSelectAll={vi.fn()}
          onDeselectAll={onDeselectAll}
        />
      </TestWrapper>,
    );

    await user.click(screen.getByRole('button', { name: 'Deselect all' }));
    expect(onDeselectAll).toHaveBeenCalledTimes(1);
  });

  it('shows "Select filtered" label when a type filter is active', () => {
    render(
      <TestWrapper>
        <ObjectSelectionSection
          allObjects={mockObjects}
          filteredObjects={[mockObjects[0], mockObjects[1]]}
          selectedObjects={new Set()}
          objectTypes={['Table', 'View', 'StoredProcedure']}
          typeFilter="Table"
          nameFilter=""
          onTypeFilterChange={vi.fn()}
          onNameFilterChange={vi.fn()}
          onToggleObject={vi.fn()}
          onSelectAll={vi.fn()}
          onDeselectAll={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByRole('button', { name: 'Select filtered' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Deselect filtered' })).toBeInTheDocument();
  });

  it('shows "Select filtered" label when a name filter is active', () => {
    render(
      <TestWrapper>
        <ObjectSelectionSection
          allObjects={mockObjects}
          filteredObjects={[mockObjects[0]]}
          selectedObjects={new Set()}
          objectTypes={['Table', 'View', 'StoredProcedure']}
          typeFilter=""
          nameFilter="Users"
          onTypeFilterChange={vi.fn()}
          onNameFilterChange={vi.fn()}
          onToggleObject={vi.fn()}
          onSelectAll={vi.fn()}
          onDeselectAll={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByRole('button', { name: 'Select filtered' })).toBeInTheDocument();
  });

  it('calls onToggleObject when an object checkbox is toggled', async () => {
    const onToggle = vi.fn();
    const user = userEvent.setup();
    render(
      <TestWrapper>
        <ObjectSelectionSection
          allObjects={mockObjects}
          filteredObjects={mockObjects}
          selectedObjects={new Set()}
          objectTypes={['Table', 'View', 'StoredProcedure']}
          typeFilter=""
          nameFilter=""
          onTypeFilterChange={vi.fn()}
          onNameFilterChange={vi.fn()}
          onToggleObject={onToggle}
          onSelectAll={vi.fn()}
          onDeselectAll={vi.fn()}
        />
      </TestWrapper>,
    );

    // Click the label text to toggle the checkbox
    await user.click(screen.getByText('dbo.Users'));
    expect(onToggle).toHaveBeenCalledWith('dbo.Users');
  });

  it('shows "No objects match the current filters" when filtered list is empty', () => {
    render(
      <TestWrapper>
        <ObjectSelectionSection
          allObjects={mockObjects}
          filteredObjects={[]}
          selectedObjects={new Set()}
          objectTypes={['Table', 'View', 'StoredProcedure']}
          typeFilter="Function"
          nameFilter=""
          onTypeFilterChange={vi.fn()}
          onNameFilterChange={vi.fn()}
          onToggleObject={vi.fn()}
          onSelectAll={vi.fn()}
          onDeselectAll={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('No objects match the current filters.')).toBeInTheDocument();
  });

  it('shows "No assessment data" message when allObjects is empty', () => {
    render(
      <TestWrapper>
        <ObjectSelectionSection
          allObjects={[]}
          filteredObjects={[]}
          selectedObjects={new Set()}
          objectTypes={[]}
          typeFilter=""
          nameFilter=""
          onTypeFilterChange={vi.fn()}
          onNameFilterChange={vi.fn()}
          onToggleObject={vi.fn()}
          onSelectAll={vi.fn()}
          onDeselectAll={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByText('No assessment data available. Run an assessment first.')).toBeInTheDocument();
  });

  it('shows filtered count summary when filters are active', () => {
    render(
      <TestWrapper>
        <ObjectSelectionSection
          allObjects={mockObjects}
          filteredObjects={[mockObjects[0], mockObjects[1]]}
          selectedObjects={new Set(['dbo.Users'])}
          objectTypes={['Table', 'View', 'StoredProcedure']}
          typeFilter="Table"
          nameFilter=""
          onTypeFilterChange={vi.fn()}
          onNameFilterChange={vi.fn()}
          onToggleObject={vi.fn()}
          onSelectAll={vi.fn()}
          onDeselectAll={vi.fn()}
        />
      </TestWrapper>,
    );
    expect(screen.getByText(/1 of 2 shown \(1 of 4 total selected\)/)).toBeInTheDocument();
  });
});