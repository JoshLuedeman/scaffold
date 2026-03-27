import { useContext } from 'react';
import { ThemeContext } from './createThemeContext';
import type { ThemeContextValue } from './createThemeContext';

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) {
    throw new Error('useTheme must be used within a ThemeProvider');
  }
  return ctx;
}
