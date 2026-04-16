import { createContext } from 'react';

export type ThemeMode = 'light' | 'dark';

export interface ThemeContextValue {
  theme: ThemeMode;
  toggleTheme: () => void;
}

export const STORAGE_KEY = 'scaffold-theme';

export const ThemeContext = createContext<ThemeContextValue | undefined>(undefined);
