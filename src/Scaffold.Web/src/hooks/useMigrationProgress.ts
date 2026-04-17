import { useState, useEffect, useRef, useCallback } from 'react';
import {
  HubConnectionBuilder,
  HubConnection,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { PublicClientApplication } from '@azure/msal-browser';
import { apiScopes } from '../auth/msalConfig';

export interface MigrationProgress {
  phase: string;
  percentComplete: number;
  currentTable: string;
  rowsProcessed: number;
  message: string;
  replicationLagBytes?: number;
}

export interface LogEntry {
  timestamp: Date;
  message: string;
}

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface UseMigrationProgressResult {
  progress: MigrationProgress | null;
  connectionStatus: ConnectionStatus;
  log: LogEntry[];
  migrationStatus: 'idle' | 'running' | 'completed' | 'failed' | 'cancelled';
}

async function getAccessToken(msalInstance: PublicClientApplication): Promise<string> {
  const accounts = msalInstance.getAllAccounts();
  if (accounts.length === 0) throw new Error('No accounts');

  try {
    const response = await msalInstance.acquireTokenSilent({
      scopes: apiScopes,
      account: accounts[0],
    });
    return response.accessToken;
  } catch {
    const response = await msalInstance.acquireTokenPopup({ scopes: apiScopes });
    return response.accessToken;
  }
}

export function useMigrationProgress(
  migrationId: string | null,
  msalInstance: PublicClientApplication | null,
): UseMigrationProgressResult {
  const [progress, setProgress] = useState<MigrationProgress | null>(null);
  const [connectionStatus, setConnectionStatus] = useState<ConnectionStatus>('disconnected');
  const [log, setLog] = useState<LogEntry[]>([]);
  const [migrationStatus, setMigrationStatus] = useState<'idle' | 'running' | 'completed' | 'failed' | 'cancelled'>('idle');
  const connectionRef = useRef<HubConnection | null>(null);

  const addLogEntry = useCallback((message: string) => {
    setLog((prev) => [...prev, { timestamp: new Date(), message }]);
  }, []);

  useEffect(() => {
    if (!migrationId) return;

    const hubUrl = `${import.meta.env.VITE_API_BASE_URL || ''}/hubs/migration`;

    const builder = new HubConnectionBuilder()
      .withUrl(hubUrl, msalInstance
        ? { accessTokenFactory: () => getAccessToken(msalInstance) }
        : {})
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning);

    const connection = builder.build();

    connectionRef.current = connection;

    connection.on('MigrationProgress', (data: MigrationProgress) => {
      setProgress(data);
      addLogEntry(data.message);
    });

    connection.on('MigrationStarted', () => {
      setMigrationStatus('running');
      addLogEntry('Migration started');
    });

    connection.on('MigrationCompleted', () => {
      setMigrationStatus('completed');
      addLogEntry('Migration completed successfully');
    });

    connection.on('MigrationFailed', (error: string) => {
      setMigrationStatus('failed');
      addLogEntry(`Migration failed: ${error}`);
    });

    connection.on('MigrationCancelled', () => {
      setMigrationStatus('cancelled');
      addLogEntry('Migration was cancelled');
    });

    connection.onreconnecting(() => {
      setConnectionStatus('reconnecting');
      addLogEntry('Connection lost, reconnecting…');
    });

    connection.onreconnected(() => {
      setConnectionStatus('connected');
      addLogEntry('Reconnected');
      // Rejoin migration group after reconnect
      connection.invoke('JoinMigration', migrationId).catch(() => {});
    });

    connection.onclose(() => {
      setConnectionStatus('disconnected');
    });

    const startConnection = async () => {
      setConnectionStatus('connecting');
      try {
        await connection.start();
        setConnectionStatus('connected');
        await connection.invoke('JoinMigration', migrationId);
      } catch (err: unknown) {
        setConnectionStatus('disconnected');
        addLogEntry(`Connection error: ${err instanceof Error ? err.message : String(err)}`);
      }
    };
    startConnection();

    return () => {
      connectionRef.current = null;
      if (connection.state !== HubConnectionState.Disconnected) {
        connection.stop();
      }
    };
  }, [migrationId, msalInstance, addLogEntry]);

  return { progress, connectionStatus, log, migrationStatus };
}
