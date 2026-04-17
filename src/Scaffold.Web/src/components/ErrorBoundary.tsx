import { Component } from 'react';
import type { ErrorInfo, ReactNode } from 'react';
import {
  Button,
  Card,
  CardHeader,
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { ErrorCircleRegular } from '@fluentui/react-icons';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'flex-start',
    paddingTop: tokens.spacingVerticalXXXL,
    paddingLeft: tokens.spacingHorizontalXL,
    paddingRight: tokens.spacingHorizontalXL,
  },
  card: {
    maxWidth: '600px',
    width: '100%',
  },
  content: {
    display: 'flex',
    flexDirection: 'column',
    rowGap: tokens.spacingVerticalM,
    padding: tokens.spacingHorizontalL,
  },
  actions: {
    display: 'flex',
    columnGap: tokens.spacingHorizontalS,
  },
  stackTrace: {
    backgroundColor: tokens.colorNeutralBackground3,
    padding: tokens.spacingHorizontalM,
    borderRadius: tokens.borderRadiusMedium,
    overflowX: 'auto',
    fontFamily: tokens.fontFamilyMonospace,
    fontSize: tokens.fontSizeBase200,
    whiteSpace: 'pre-wrap',
    wordBreak: 'break-word',
    maxHeight: '300px',
    overflowY: 'auto',
  },
});

interface ErrorBoundaryProps {
  children: ReactNode;
  fallback?: ReactNode;
  onReset?: () => void;
}

interface ErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
}

/**
 * Styled fallback component displayed when an error is caught.
 * Extracted as a function component so it can use Fluent UI's `makeStyles` hook.
 */
function ErrorFallback({
  error,
  onReset,
}: {
  error: Error;
  onReset: () => void;
}) {
  const styles = useStyles();

  return (
    <div className={styles.container}>
      <Card className={styles.card}>
        <CardHeader
          image={<ErrorCircleRegular fontSize={24} />}
          header={<Text weight="semibold" size={500}>Something went wrong</Text>}
        />
        <div className={styles.content}>
          <MessageBar intent="error">
            <MessageBarBody>
              <MessageBarTitle>Error</MessageBarTitle>
              {error.message || 'An unexpected error occurred.'}
            </MessageBarBody>
          </MessageBar>

          <div className={styles.actions}>
            <Button appearance="primary" onClick={onReset}>
              Try Again
            </Button>
            <Button
              appearance="secondary"
              as="a"
              href="/"
            >
              Go Home
            </Button>
          </div>

          {import.meta.env.DEV && error.stack && (
            <div className={styles.stackTrace}>
              {error.stack}
            </div>
          )}
        </div>
      </Card>
    </div>
  );
}

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    console.error('ErrorBoundary caught an error:', error, errorInfo);
  }

  handleReset = () => {
    this.setState({ hasError: false, error: null });
    this.props.onReset?.();
  };

  render() {
    if (this.state.hasError && this.state.error) {
      if (this.props.fallback) {
        return this.props.fallback;
      }

      return (
        <ErrorFallback
          error={this.state.error}
          onReset={this.handleReset}
        />
      );
    }

    return this.props.children;
  }
}

export default ErrorBoundary;
