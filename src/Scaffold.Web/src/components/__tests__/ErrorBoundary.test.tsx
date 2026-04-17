import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FluentProvider, webLightTheme } from '@fluentui/react-components';
import { MemoryRouter } from 'react-router-dom';
import { ErrorBoundary } from '../ErrorBoundary';

function ThrowingComponent({ error }: { error: Error }) {
  throw error;
}

function GoodComponent() {
  return <div>Everything is fine</div>;
}

function Wrapper({ children }: { children: React.ReactNode }) {
  return (
    <FluentProvider theme={webLightTheme}>
      <MemoryRouter>
        {children}
      </MemoryRouter>
    </FluentProvider>
  );
}

// Suppress console.error for expected error boundary logging
const originalConsoleError = console.error;
beforeEach(() => {
  console.error = vi.fn();
});
afterEach(() => {
  console.error = originalConsoleError;
});

describe('ErrorBoundary', () => {
  it('renders children when no error occurs', () => {
    render(
      <Wrapper>
        <ErrorBoundary>
          <GoodComponent />
        </ErrorBoundary>
      </Wrapper>,
    );

    expect(screen.getByText('Everything is fine')).toBeInTheDocument();
  });

  it('renders error fallback when a child throws', () => {
    render(
      <Wrapper>
        <ErrorBoundary>
          <ThrowingComponent error={new Error('Test error message')} />
        </ErrorBoundary>
      </Wrapper>,
    );

    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
    expect(screen.getByText('Test error message')).toBeInTheDocument();
  });

  it('renders Try Again button that calls onReset', async () => {
    const user = userEvent.setup();
    const onReset = vi.fn();

    render(
      <Wrapper>
        <ErrorBoundary onReset={onReset}>
          <ThrowingComponent error={new Error('Boom')} />
        </ErrorBoundary>
      </Wrapper>,
    );

    expect(screen.getByText('Something went wrong')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Try Again' }));

    expect(onReset).toHaveBeenCalledTimes(1);
  });

  it('renders Go Home link pointing to /', () => {
    render(
      <Wrapper>
        <ErrorBoundary>
          <ThrowingComponent error={new Error('Broken')} />
        </ErrorBoundary>
      </Wrapper>,
    );

    const goHomeLink = screen.getByRole('link', { name: 'Go Home' });
    expect(goHomeLink).toBeInTheDocument();
    expect(goHomeLink).toHaveAttribute('href', '/');
  });

  it('renders custom fallback when provided', () => {
    render(
      <Wrapper>
        <ErrorBoundary fallback={<div>Custom error UI</div>}>
          <ThrowingComponent error={new Error('Crash')} />
        </ErrorBoundary>
      </Wrapper>,
    );

    expect(screen.getByText('Custom error UI')).toBeInTheDocument();
    expect(screen.queryByText('Something went wrong')).not.toBeInTheDocument();
  });

  it('renders the Error message bar with details', () => {
    render(
      <Wrapper>
        <ErrorBoundary>
          <ThrowingComponent error={new Error('Detailed failure info')} />
        </ErrorBoundary>
      </Wrapper>,
    );

    expect(screen.getByText('Error')).toBeInTheDocument();
    expect(screen.getByText('Detailed failure info')).toBeInTheDocument();
  });
});
