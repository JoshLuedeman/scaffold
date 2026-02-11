import type { AssessmentReport as Report } from '../types';
import './AssessmentReport.css';

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

export default function AssessmentReport({ report }: { report: Report }) {
  const { schema, dataProfile, performance, compatibilityIssues, recommendation, compatibilityScore, risk } = report;
  const blockingCount = compatibilityIssues.filter((i) => i.isBlocking).length;

  return (
    <div className="assessment-report">
      <div className="report-summary">
        <div className="metric-card">
          <div className="label">Tables</div>
          <div className="value">{schema.tableCount}</div>
        </div>
        <div className="metric-card">
          <div className="label">Views</div>
          <div className="value">{schema.viewCount}</div>
        </div>
        <div className="metric-card">
          <div className="label">Stored Procs</div>
          <div className="value">{schema.storedProcedureCount}</div>
        </div>
        <div className="metric-card">
          <div className="label">Total Rows</div>
          <div className="value">{dataProfile.totalRowCount.toLocaleString()}</div>
        </div>
        <div className="metric-card">
          <div className="label">Total Size</div>
          <div className="value">{formatBytes(dataProfile.totalSizeBytes)}</div>
        </div>
        <div className="metric-card">
          <div className="label">Compatibility</div>
          <div className="value">{compatibilityScore}%</div>
        </div>
        <div className="metric-card">
          <div className="label">Risk</div>
          <div className="value">
            <span className={`risk-badge risk-${risk.toLowerCase()}`}>{risk}</span>
          </div>
        </div>
      </div>

      <h3 className="section-heading">Performance Profile</h3>
      <div className="report-summary">
        <div className="metric-card">
          <div className="label">Avg CPU</div>
          <div className="value">{performance.avgCpuPercent.toFixed(1)}%</div>
        </div>
        <div className="metric-card">
          <div className="label">Memory Used</div>
          <div className="value">{performance.memoryUsedMb} MB</div>
        </div>
        <div className="metric-card">
          <div className="label">Avg I/O</div>
          <div className="value">{performance.avgIoMbPerSecond.toFixed(1)} MB/s</div>
        </div>
        <div className="metric-card">
          <div className="label">Max DB Size</div>
          <div className="value">{performance.maxDatabaseSizeMb} MB</div>
        </div>
      </div>

      <h3 className="section-heading">Tier Recommendation</h3>
      <div className="recommendation-card">
        <h4>{recommendation.serviceTier} — {recommendation.computeSize}</h4>
        <dl className="recommendation-details">
          {recommendation.vCores != null && (
            <>
              <dt>vCores</dt>
              <dd>{recommendation.vCores}</dd>
            </>
          )}
          {recommendation.dtus != null && (
            <>
              <dt>DTUs</dt>
              <dd>{recommendation.dtus}</dd>
            </>
          )}
          <dt>Storage</dt>
          <dd>{recommendation.storageGb} GB</dd>
          <dt>Est. Monthly Cost</dt>
          <dd>${recommendation.estimatedMonthlyCostUsd.toFixed(2)}</dd>
        </dl>
        {recommendation.reasoning && (
          <p className="recommendation-reasoning">{recommendation.reasoning}</p>
        )}
      </div>

      <h3 className="section-heading">
        Compatibility Issues ({compatibilityIssues.length})
        {blockingCount > 0 && <span className="severity-blocking"> — {blockingCount} blocking</span>}
      </h3>
      {compatibilityIssues.length === 0 ? (
        <p className="no-issues">No compatibility issues found.</p>
      ) : (
        <table className="issues-table">
          <thead>
            <tr>
              <th>Object</th>
              <th>Type</th>
              <th>Description</th>
              <th>Severity</th>
            </tr>
          </thead>
          <tbody>
            {compatibilityIssues.map((issue, i) => (
              <tr key={i}>
                <td>{issue.objectName}</td>
                <td>{issue.issueType}</td>
                <td>{issue.description}</td>
                <td>
                  <span className={issue.isBlocking ? 'severity-blocking' : 'severity-nonblocking'}>
                    {issue.isBlocking ? 'Blocking' : 'Non-blocking'}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
