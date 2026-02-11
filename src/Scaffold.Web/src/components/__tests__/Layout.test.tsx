import { render, screen } from '@testing-library/react';
import { TestWrapper } from '../../test/msalMock';
import Layout from '../Layout';

describe('Layout', () => {
  it('renders the app title', () => {
    render(
      <TestWrapper>
        <Layout />
      </TestWrapper>,
    );
    expect(screen.getByText('Scaffold')).toBeInTheDocument();
  });

  it('renders the Projects navigation link', () => {
    render(
      <TestWrapper>
        <Layout />
      </TestWrapper>,
    );
    expect(screen.getByRole('link', { name: 'Projects' })).toBeInTheDocument();
  });

  it('renders the logout button', () => {
    render(
      <TestWrapper>
        <Layout />
      </TestWrapper>,
    );
    expect(screen.getByRole('button', { name: 'Logout' })).toBeInTheDocument();
  });

  it('displays the username', () => {
    render(
      <TestWrapper>
        <Layout />
      </TestWrapper>,
    );
    expect(screen.getByText('Test User')).toBeInTheDocument();
  });
});
