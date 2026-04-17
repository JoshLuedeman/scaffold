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
  if (accounts.length === 0) {
    return { name: 'Developer', logout: undefined };
  }
  return {
    name: accounts[0]?.name ?? accounts[0]?.username,
    logout: () => instance.logoutRedirect(),
  };
}

const useStyles = makeStyles({
  skipNav: {
    position: 'absolute',
    left: '-9999px',
    top: 'auto',
    width: '1px',
    height: '1px',
    overflow: 'hidden',
    ':focus': {
      position: 'fixed',
      top: '0',
      left: '0',
      width: 'auto',
      height: 'auto',
      padding: tokens.spacingVerticalS + ' ' + tokens.spacingHorizontalM,
      backgroundColor: tokens.colorBrandBackground,
      color: tokens.colorNeutralForegroundOnBrand,
      zIndex: 1000,
      fontSize: tokens.fontSizeBase300,
      fontWeight: tokens.fontWeightSemibold,
      textDecorationLine: 'none',
    },
  },
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
      <a href="#main-content" className={styles.skipNav}>
        Skip to main content
      </a>
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
        <main id="main-content" className={styles.content}>
          <Outlet />
        </main>
      </div>
    </div>
  );
}
