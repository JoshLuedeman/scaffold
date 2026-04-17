import { BrowserRouter, Routes, Route } from 'react-router-dom';
import {
  MsalProvider,
  AuthenticatedTemplate,
  UnauthenticatedTemplate,
  useMsal,
} from '@azure/msal-react';
import type { PublicClientApplication } from '@azure/msal-browser';
import Layout from './components/Layout';
import { ErrorBoundary } from './components/ErrorBoundary';
import ProjectList from './pages/ProjectList';
import ProjectDetail from './pages/ProjectDetail';
import AssessmentWizard from './pages/AssessmentWizard';
import MigrationConfig from './pages/MigrationConfig';
import MigrationExecution from './pages/MigrationExecution';
import { loginRequest } from './auth/msalConfig';

function LoginPage() {
  const { instance } = useMsal();
  const handleLogin = () => instance.loginRedirect(loginRequest);

  return (
    <div className="login-page">
      <h1>Scaffold</h1>
      <p>Database migration management for Azure</p>
      <button className="btn-login" onClick={handleLogin}>
        Sign in with Microsoft
      </button>
    </div>
  );
}

function AppRoutes() {
  return (
    <BrowserRouter>
      <ErrorBoundary>
        <Routes>
          <Route element={<Layout />}>
            <Route path="/" element={<ErrorBoundary><ProjectList /></ErrorBoundary>} />
            <Route path="/projects/:id" element={<ErrorBoundary><ProjectDetail /></ErrorBoundary>} />
            <Route path="/projects/:id/assess" element={<ErrorBoundary><AssessmentWizard /></ErrorBoundary>} />
            <Route path="/projects/:id/configure" element={<ErrorBoundary><MigrationConfig /></ErrorBoundary>} />
            <Route path="/projects/:id/execute" element={<ErrorBoundary><MigrationExecution /></ErrorBoundary>} />
          </Route>
        </Routes>
      </ErrorBoundary>
    </BrowserRouter>
  );
}

function App({ msalInstance }: { msalInstance: PublicClientApplication | null }) {
  if (!msalInstance) {
    return <AppRoutes />;
  }

  return (
    <MsalProvider instance={msalInstance}>
      <AuthenticatedTemplate>
        <AppRoutes />
      </AuthenticatedTemplate>
      <UnauthenticatedTemplate>
        <LoginPage />
      </UnauthenticatedTemplate>
    </MsalProvider>
  );
}

export default App;
