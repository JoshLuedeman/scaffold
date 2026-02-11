import { useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import './ProjectDetail.css';

type Phase = 'assess' | 'define' | 'execute';

export default function ProjectDetail() {
  const { id } = useParams<{ id: string }>();
  const [activeTab, setActiveTab] = useState<Phase>('assess');

  const tabs: { key: Phase; label: string }[] = [
    { key: 'assess', label: 'Assess' },
    { key: 'define', label: 'Define' },
    { key: 'execute', label: 'Execute' },
  ];

  return (
    <div className="project-detail">
      <nav className="breadcrumb">
        <Link to="/">Projects</Link> <span>/</span> <span>Project {id}</span>
      </nav>

      <h2>Project {id}</h2>

      <div className="tabs">
        {tabs.map((t) => (
          <button
            key={t.key}
            className={`tab ${activeTab === t.key ? 'tab-active' : ''}`}
            onClick={() => setActiveTab(t.key)}
          >
            {t.label}
          </button>
        ))}
      </div>

      <div className="tab-content">
        {activeTab === 'assess' && (
          <div className="placeholder">
            <h3>Assessment</h3>
            <p>Configure source connection and run compatibility assessment.</p>
            <Link to={`/projects/${id}/assess`} className="btn-start-assess">
              Start Assessment Wizard →
            </Link>
          </div>
        )}
        {activeTab === 'define' && (
          <div className="placeholder">
            <h3>Migration Plan</h3>
            <p>Review assessment results and define the migration strategy.</p>
            <Link to={`/projects/${id}/configure`} className="btn-start-assess">
              Configure Migration →
            </Link>
          </div>
        )}
        {activeTab === 'execute' && (
          <div className="placeholder">
            <h3>Execution</h3>
            <p>Execute the migration and monitor progress.</p>
            <Link to={`/projects/${id}/execute`} className="btn-start-assess">
              Execute Migration →
            </Link>
          </div>
        )}
      </div>
    </div>
  );
}
