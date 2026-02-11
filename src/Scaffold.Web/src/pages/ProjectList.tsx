import { Link } from 'react-router-dom';
import type { MigrationProject, ProjectStatus } from '../types';
import './ProjectList.css';

const mockProjects: MigrationProject[] = [
  {
    id: '1',
    name: 'Northwind DB',
    description: 'Legacy ERP database migration',
    status: 'Assessed',
    createdBy: 'admin@contoso.com',
    createdAt: '2025-01-15T10:30:00Z',
    updatedAt: '2025-01-20T14:00:00Z',
  },
  {
    id: '2',
    name: 'Inventory System',
    description: 'Warehouse inventory SQL Server',
    status: 'Created',
    createdBy: 'admin@contoso.com',
    createdAt: '2025-02-01T09:00:00Z',
    updatedAt: '2025-02-01T09:00:00Z',
  },
  {
    id: '3',
    name: 'Customer Portal',
    description: 'Customer-facing app database',
    status: 'MigrationComplete',
    createdBy: 'admin@contoso.com',
    createdAt: '2024-11-05T08:00:00Z',
    updatedAt: '2025-01-10T16:45:00Z',
  },
];

const statusBadge: Record<ProjectStatus, string> = {
  Created: 'badge-default',
  Assessing: 'badge-info',
  Assessed: 'badge-info',
  PlanningMigration: 'badge-warning',
  MigrationPlanned: 'badge-warning',
  Migrating: 'badge-warning',
  MigrationComplete: 'badge-success',
  Failed: 'badge-danger',
};

export default function ProjectList() {
  return (
    <div className="project-list">
      <div className="page-header">
        <h2>Projects</h2>
      </div>
      <table className="table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Status</th>
            <th>Created</th>
            <th>Updated</th>
          </tr>
        </thead>
        <tbody>
          {mockProjects.map((p) => (
            <tr key={p.id}>
              <td>
                <Link to={`/projects/${p.id}`}>{p.name}</Link>
                {p.description && <span className="desc">{p.description}</span>}
              </td>
              <td>
                <span className={`badge ${statusBadge[p.status]}`}>{p.status}</span>
              </td>
              <td>{new Date(p.createdAt).toLocaleDateString()}</td>
              <td>{new Date(p.updatedAt).toLocaleDateString()}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
