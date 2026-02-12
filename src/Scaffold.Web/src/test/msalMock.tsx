import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { ThemeProvider } from '../theme/ThemeContext';

// Mock MSAL hooks
vi.mock('@azure/msal-react', () => ({
  MsalProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
  AuthenticatedTemplate: ({ children }: { children: ReactNode }) => <>{children}</>,
  UnauthenticatedTemplate: ({ children }: { children: ReactNode }) => <>{children}</>,
  useMsal: () => ({
    instance: {
      logoutRedirect: vi.fn(),
      loginRedirect: vi.fn(),
      acquireTokenSilent: vi.fn().mockResolvedValue({ accessToken: 'mock-token' }),
      getAllAccounts: () => [{ name: 'Test User', username: 'test@contoso.com' }],
    },
    accounts: [{ name: 'Test User', username: 'test@contoso.com' }],
    inProgress: 'none',
  }),
}));

export function TestWrapper({ children, initialEntries = ['/'] }: { children: ReactNode; initialEntries?: string[] }) {
  return (
    <ThemeProvider>
      <MemoryRouter initialEntries={initialEntries}>{children}</MemoryRouter>
    </ThemeProvider>
  );
}
