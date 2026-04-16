import { useContext } from 'react';
import { ThemeContext } from './themeContextValue';
import type { ThemeContextValue } from './themeContextValue';

export type { ThemeMode, ThemeContextValue } from './themeContextValue';

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) {
    throw new Error('useTheme must be used within a ThemeProvider');
  }
  return ctx;
}
