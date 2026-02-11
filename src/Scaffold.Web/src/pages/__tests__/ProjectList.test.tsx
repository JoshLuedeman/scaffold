import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import ProjectList from '../ProjectList';

describe('ProjectList', () => {
  it('renders the project list heading', () => {
    render(
      <MemoryRouter>
        <ProjectList />
      </MemoryRouter>,
    );
    expect(screen.getByRole('heading', { name: 'Projects' })).toBeInTheDocument();
  });

  it('displays mock project names', () => {
    render(
      <MemoryRouter>
        <ProjectList />
      </MemoryRouter>,
    );
    expect(screen.getByText('Northwind DB')).toBeInTheDocument();
    expect(screen.getByText('Inventory System')).toBeInTheDocument();
    expect(screen.getByText('Customer Portal')).toBeInTheDocument();
  });

  it('displays project status badges', () => {
    render(
      <MemoryRouter>
        <ProjectList />
      </MemoryRouter>,
    );
    expect(screen.getByText('Assessed')).toBeInTheDocument();
    expect(screen.getByText('MigrationComplete')).toBeInTheDocument();
    // 'Created' appears as both a table header and a status badge
    const badges = screen.getAllByText('Created');
    expect(badges.some((el) => el.classList.contains('badge'))).toBe(true);
  });

  it('renders project links to detail pages', () => {
    render(
      <MemoryRouter>
        <ProjectList />
      </MemoryRouter>,
    );
    const links = screen.getAllByRole('link');
    expect(links.some((l) => l.getAttribute('href') === '/projects/1')).toBe(true);
    expect(links.some((l) => l.getAttribute('href') === '/projects/2')).toBe(true);
    expect(links.some((l) => l.getAttribute('href') === '/projects/3')).toBe(true);
  });

  it('renders the table with correct headers', () => {
    render(
      <MemoryRouter>
        <ProjectList />
      </MemoryRouter>,
    );
    expect(screen.getByRole('columnheader', { name: 'Name' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Status' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Created' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Updated' })).toBeInTheDocument();
  });
});
