import { useMsal } from '@azure/msal-react';
import { NavLink, Outlet } from 'react-router-dom';
import './Layout.css';

export default function Layout() {
  const { instance, accounts } = useMsal();
  const account = accounts[0];

  const handleLogout = () => {
    instance.logoutRedirect();
  };

  return (
    <div className="layout">
      <header className="header">
        <h1 className="header-title">Scaffold</h1>
        <div className="header-user">
          {account && <span className="username">{account.name ?? account.username}</span>}
          <button className="btn btn-logout" onClick={handleLogout}>
            Logout
          </button>
        </div>
      </header>
      <div className="layout-body">
        <nav className="sidebar">
          <ul>
            <li>
              <NavLink to="/" end>
                Projects
              </NavLink>
            </li>
          </ul>
        </nav>
        <main className="content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
