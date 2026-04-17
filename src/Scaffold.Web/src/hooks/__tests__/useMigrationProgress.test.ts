import { renderHook, act } from '@testing-library/react';
import { useMigrationProgress } from '../useMigrationProgress';

// Capture event handlers registered by the hook
const eventHandlers = new Map<string, (...args: unknown[]) => void>();
let reconnectingHandler: (() => void) | null = null;
let reconnectedHandler: (() => void) | null = null;
let closeHandler: (() => void) | null = null;
let connectionState = 'Disconnected';

const mockStart = vi.fn().mockResolvedValue(undefined);
const mockStop = vi.fn().mockResolvedValue(undefined);
const mockInvoke = vi.fn().mockResolvedValue(undefined);

function createMockConnection() {
  return {
    on: vi.fn((event: string, handler: (...args: unknown[]) => void) => {
      eventHandlers.set(event, handler);
    }),
    start: mockStart,
    stop: mockStop,
    invoke: mockInvoke,
    onreconnecting: vi.fn((handler: () => void) => { reconnectingHandler = handler; }),
    onreconnected: vi.fn((handler: () => void) => { reconnectedHandler = handler; }),
    onclose: vi.fn((handler: () => void) => { closeHandler = handler; }),
    get state() { return connectionState; },
  };
}

vi.mock('@microsoft/signalr', () => {
  class MockHubConnectionBuilder {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() { return createMockConnection(); }
  }

  return {
    HubConnectionBuilder: MockHubConnectionBuilder,
    HubConnectionState: {
      Disconnected: 'Disconnected',
      Connected: 'Connected',
      Connecting: 'Connecting',
      Reconnecting: 'Reconnecting',
    },
    LogLevel: {
      Warning: 3,
    },
  };
});

describe('useMigrationProgress', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    eventHandlers.clear();
    reconnectingHandler = null;
    reconnectedHandler = null;
    closeHandler = null;
    connectionState = 'Disconnected';
    mockStart.mockResolvedValue(undefined);
    mockInvoke.mockResolvedValue(undefined);
  });

  it('returns idle state when no migrationId is provided', () => {
    const { result } = renderHook(() => useMigrationProgress(null, null));

    expect(result.current.progress).toBeNull();
    expect(result.current.connectionStatus).toBe('disconnected');
    expect(result.current.migrationStatus).toBe('idle');
    expect(result.current.log).toEqual([]);
  });

  it('does not connect when migrationId is null', () => {
    renderHook(() => useMigrationProgress(null, null));
    expect(mockStart).not.toHaveBeenCalled();
  });

  it('connects to SignalR hub when migrationId is provided', async () => {
    renderHook(() => useMigrationProgress('mig-1', null));

    await vi.waitFor(() => {
      expect(mockStart).toHaveBeenCalled();
    });
  });

  it('registers MigrationProgress event handler', () => {
    renderHook(() => useMigrationProgress('mig-1', null));
    expect(eventHandlers.has('MigrationProgress')).toBe(true);
  });

  it('registers MigrationStarted event handler', () => {
    renderHook(() => useMigrationProgress('mig-1', null));
    expect(eventHandlers.has('MigrationStarted')).toBe(true);
  });

  it('registers MigrationCompleted event handler', () => {
    renderHook(() => useMigrationProgress('mig-1', null));
    expect(eventHandlers.has('MigrationCompleted')).toBe(true);
  });

  it('registers MigrationFailed event handler', () => {
    renderHook(() => useMigrationProgress('mig-1', null));
    expect(eventHandlers.has('MigrationFailed')).toBe(true);
  });

  it('registers MigrationCancelled event handler', () => {
    renderHook(() => useMigrationProgress('mig-1', null));
    expect(eventHandlers.has('MigrationCancelled')).toBe(true);
  });

  it('invokes JoinMigration after successful connection', async () => {
    renderHook(() => useMigrationProgress('mig-1', null));

    await vi.waitFor(() => {
      expect(mockInvoke).toHaveBeenCalledWith('JoinMigration', 'mig-1');
    });
  });

  it('updates progress when MigrationProgress event fires', async () => {
    const { result } = renderHook(() => useMigrationProgress('mig-1', null));

    await vi.waitFor(() => {
      expect(eventHandlers.has('MigrationProgress')).toBe(true);
    });

    act(() => {
      eventHandlers.get('MigrationProgress')!({
        phase: 'Data Transfer',
        percentComplete: 45,
        currentTable: 'dbo.Users',
        rowsProcessed: 500,
        message: 'Migrating dbo.Users',
      });
    });

    expect(result.current.progress).toEqual({
      phase: 'Data Transfer',
      percentComplete: 45,
      currentTable: 'dbo.Users',
      rowsProcessed: 500,
      message: 'Migrating dbo.Users',
    });
    expect(result.current.log.length).toBeGreaterThan(0);
    expect(result.current.log[result.current.log.length - 1].message).toBe('Migrating dbo.Users');
  });

  it('updates migrationStatus to running when MigrationStarted fires', async () => {
    const { result } = renderHook(() => useMigrationProgress('mig-1', null));

    await vi.waitFor(() => {
      expect(eventHandlers.has('MigrationStarted')).toBe(true);
    });

    act(() => {
      eventHandlers.get('MigrationStarted')!();
    });

    expect(result.current.migrationStatus).toBe('running');
  });

  it('updates migrationStatus to completed when MigrationCompleted fires', async () => {
    const { result } = renderHook(() => useMigrationProgress('mig-1', null));

    await vi.waitFor(() => {
      expect(eventHandlers.has('MigrationCompleted')).toBe(true);
    });

    act(() => {
      eventHandlers.get('MigrationCompleted')!();
    });

    expect(result.current.migrationStatus).toBe('completed');
  });

  it('updates migrationStatus to failed when MigrationFailed fires', async () => {
    const { result } = renderHook(() => useMigrationProgress('mig-1', null));

    await vi.waitFor(() => {
      expect(eventHandlers.has('MigrationFailed')).toBe(true);
    });

    act(() => {
      eventHandlers.get('MigrationFailed')!('Database timeout');
    });

    expect(result.current.migrationStatus).toBe('failed');
    expect(result.current.log[result.current.log.length - 1].message).toContain('Database timeout');
  });

  it('updates migrationStatus to cancelled when MigrationCancelled fires', async () => {
    const { result } = renderHook(() => useMigrationProgress('mig-1', null));

    await vi.waitFor(() => {
      expect(eventHandlers.has('MigrationCancelled')).toBe(true);
    });

    act(() => {
      eventHandlers.get('MigrationCancelled')!();
    });

    expect(result.current.migrationStatus).toBe('cancelled');
    expect(result.current.log[result.current.log.length - 1].message).toBe('Migration was cancelled');
  });

  it('handles reconnecting event', async () => {
    const { result } = renderHook(() => useMigrationProgress('mig-1', null));

    await vi.waitFor(() => {
      expect(reconnectingHandler).not.toBeNull();
    });

    act(() => {
      reconnectingHandler!();
    });

    expect(result.current.connectionStatus).toBe('reconnecting');
  });

  it('handles reconnected event and rejoins migration group', async () => {
    renderHook(() => useMigrationProgress('mig-1', null));

    await vi.waitFor(() => {
      expect(reconnectedHandler).not.toBeNull();
    });

    act(() => {
      reconnectedHandler!();
    });

    // Should invoke JoinMigration again after reconnect
    const joinCalls = mockInvoke.mock.calls.filter(
      (call: unknown[]) => call[0] === 'JoinMigration',
    );
    expect(joinCalls.length).toBeGreaterThanOrEqual(2);
  });

  it('handles close event', async () => {
    const { result } = renderHook(() => useMigrationProgress('mig-1', null));

    await vi.waitFor(() => {
      expect(closeHandler).not.toBeNull();
    });

    act(() => {
      closeHandler!();
    });

    expect(result.current.connectionStatus).toBe('disconnected');
  });

  it('stops connection on unmount when connected', async () => {
    connectionState = 'Connected';
    const { unmount } = renderHook(() => useMigrationProgress('mig-1', null));

    await vi.waitFor(() => {
      expect(mockStart).toHaveBeenCalled();
    });

    unmount();

    expect(mockStop).toHaveBeenCalled();
  });

  it('handles connection start failure gracefully', async () => {
    mockStart.mockRejectedValueOnce(new Error('Connection refused'));

    const { result } = renderHook(() => useMigrationProgress('mig-1', null));

    await vi.waitFor(() => {
      expect(result.current.connectionStatus).toBe('disconnected');
    });

    expect(result.current.log.some((e) => e.message.includes('Connection refused'))).toBe(true);
  });
});