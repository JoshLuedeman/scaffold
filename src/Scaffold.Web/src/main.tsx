import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { PublicClientApplication, EventType } from '@azure/msal-browser';
import { msalConfig } from './auth/msalConfig';
import { setMsalInstance } from './services/api';
import { ThemeProvider } from './theme/ThemeContext';
import './index.css';
import App from './App';

const DEV_AUTH_DISABLED = import.meta.env.VITE_DISABLE_AUTH === 'true';

if (DEV_AUTH_DISABLED) {
  // Skip MSAL entirely — render the app without auth
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <ThemeProvider>
        <App msalInstance={null} />
      </ThemeProvider>
    </StrictMode>,
  );
} else {
  const msalInstance = new PublicClientApplication(msalConfig);

  // Set active account after login redirect
  msalInstance.initialize().then(() => {
    const accounts = msalInstance.getAllAccounts();
    if (accounts.length > 0) {
      msalInstance.setActiveAccount(accounts[0]);
    }

    msalInstance.addEventCallback((event) => {
      if (event.eventType === EventType.LOGIN_SUCCESS && event.payload) {
        const payload = event.payload as { account: Parameters<typeof msalInstance.setActiveAccount>[0] };
        msalInstance.setActiveAccount(payload.account);
      }
    });

    setMsalInstance(msalInstance);

    createRoot(document.getElementById('root')!).render(
      <StrictMode>
        <ThemeProvider>
          <App msalInstance={msalInstance} />
        </ThemeProvider>
      </StrictMode>,
    );
  });
}
