import { useMsal } from '@azure/msal-react';
import { NavLink, Outlet } from 'react-router-dom';
import {
  Button,
  Divider,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import {
  WeatherMoonRegular,
  WeatherSunnyRegular,
  FolderRegular,
} from '@fluentui/react-icons';
import { useTheme } from '../theme/useTheme';

function useAuth() {
  const { instance, accounts } = useMsal();
  try {
    return {
      name: accounts[0]?.name ?? accounts[0]?.username,
      logout: () => instance.logoutRedirect(),
    };
  } catch {
    return { name: 'Developer', logout: undefined };
  }
}

const useStyles = makeStyles({
  layout: {
    display: 'flex',
    flexDirection: 'column',
    minHeight: '100vh',
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingLeft: tokens.spacingHorizontalXL,
    paddingRight: tokens.spacingHorizontalXL,
    height: '48px',
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
  },
  headerTitle: {
    fontSize: tokens.fontSizeBase400,
    fontWeight: tokens.fontWeightSemibold,
    color: 'inherit',
  },
  headerActions: {
    display: 'flex',
    alignItems: 'center',
    columnGap: tokens.spacingHorizontalM,
  },
  headerButton: {
    color: 'inherit',
  },
  username: {
    fontSize: tokens.fontSizeBase200,
    opacity: 0.85,
    color: 'inherit',
  },
  body: {
    display: 'flex',
    flex: '1',
  },
  sidebar: {
    width: '220px',
    backgroundColor: tokens.colorNeutralBackground2,
    borderRight: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke1}`,
    paddingTop: tokens.spacingVerticalM,
    display: 'flex',
    flexDirection: 'column',
  },
  navList: {
    listStyleType: 'none',
    margin: '0',
    padding: '0',
  },
  navLink: {
    display: 'flex',
    alignItems: 'center',
    columnGap: tokens.spacingHorizontalS,
    paddingTop: tokens.spacingVerticalS,
    paddingBottom: tokens.spacingVerticalS,
    paddingLeft: tokens.spacingHorizontalXL,
    paddingRight: tokens.spacingHorizontalXL,
    color: tokens.colorNeutralForeground1,
    textDecorationLine: 'none',
    fontSize: tokens.fontSizeBase300,
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground1Hover,
    },
  },
  navLinkActive: {
    backgroundColor: tokens.colorNeutralBackground1Selected,
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorBrandForeground1,
  },
  content: {
    flex: '1',
    padding: tokens.spacingHorizontalXXL,
    backgroundColor: tokens.colorNeutralBackground1,
  },
});

export default function Layout() {
  const styles = useStyles();
  const { name, logout } = useAuth();
  const { theme, toggleTheme } = useTheme();

  return (
    <div className={styles.layout}>
      <header className={styles.header}>
        <Text className={styles.headerTitle}>Scaffold</Text>
        <div className={styles.headerActions}>
          <Button
            appearance="transparent"
            className={styles.headerButton}
            icon={
              theme === 'light' ? (
                <WeatherMoonRegular />
              ) : (
                <WeatherSunnyRegular />
              )
            }
            onClick={toggleTheme}
            aria-label="Toggle theme"
          />
          {name && <Text className={styles.username}>{name}</Text>}
          {logout && (
            <Button
              appearance="transparent"
              className={styles.headerButton}
              onClick={logout}
            >
              Logout
            </Button>
          )}
        </div>
      </header>
      <div className={styles.body}>
        <nav className={styles.sidebar}>
          <ul className={styles.navList}>
            <li>
              <NavLink
                to="/"
                end
                className={({ isActive }) =>
                  `${styles.navLink}${isActive ? ` ${styles.navLinkActive}` : ''}`
                }
              >
                <FolderRegular />
                Projects
              </NavLink>
            </li>
          </ul>
          <Divider />
        </nav>
        <main className={styles.content}>
          <Outlet />
        </main>
      </div>
    </div>
  );
}
