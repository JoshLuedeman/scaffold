import { PublicClientApplication } from '@azure/msal-browser';
import { apiScopes } from '../auth/msalConfig';

const BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api';

const MAX_RETRIES = 3;
const BASE_BACKOFF_MS = 1000;

let msalInstance: PublicClientApplication | null = null;

export function setMsalInstance(instance: PublicClientApplication) {
  msalInstance = instance;
}

/**
 * Structured API error based on RFC 7807 ProblemDetails.
 */
export class ApiError extends Error {
  status: number;
  title: string;
  detail: string;
  instance?: string;

  constructor(status: number, title: string, detail: string, instance?: string) {
    super(detail || title);
    this.name = 'ApiError';
    this.status = status;
    this.title = title;
    this.detail = detail;
    this.instance = instance;
  }
}

async function getAccessToken(): Promise<string | null> {
  if (!msalInstance) return null;

  const accounts = msalInstance.getAllAccounts();
  if (accounts.length === 0) return null;

  try {
    const response = await msalInstance.acquireTokenSilent({
      scopes: apiScopes,
      account: accounts[0],
    });
    return response.accessToken;
  } catch {
    // If silent acquisition fails, trigger interactive login
    const response = await msalInstance.acquireTokenPopup({
      scopes: apiScopes,
    });
    return response.accessToken;
  }
}

function isRetryable(status: number): boolean {
  return status >= 500;
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function parseErrorResponse(response: Response): Promise<ApiError> {
  try {
    const contentType = response.headers.get('content-type') ?? '';
    if (contentType.includes('application/problem+json') || contentType.includes('application/json')) {
      const body = await response.json() as Record<string, unknown>;
      return new ApiError(
        (body.status as number) ?? response.status,
        (body.title as string) ?? response.statusText,
        (body.detail as string) ?? '',
        body.instance as string | undefined,
      );
    }
  } catch {
    // Fall through to default error
  }

  return new ApiError(
    response.status,
    response.statusText,
    `API error: ${response.status} ${response.statusText}`,
  );
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = await getAccessToken();

  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...options.headers,
  };

  if (token) {
    (headers as Record<string, string>)['Authorization'] = `Bearer ${token}`;
  }

  let lastError: Error | undefined;

  for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
    try {
      const response = await fetch(`${BASE_URL}${path}`, {
        ...options,
        headers,
      });

      if (response.ok) {
        if (response.status === 204 || response.headers.get('content-length') === '0') {
          return undefined as T;
        }
        return response.json() as Promise<T>;
      }

      const apiError = await parseErrorResponse(response);

      // Never retry 4xx errors
      if (!isRetryable(response.status)) {
        throw apiError;
      }

      lastError = apiError;
    } catch (error) {
      // If it's already an ApiError with a 4xx status, don't retry
      if (error instanceof ApiError && !isRetryable(error.status)) {
        throw error;
      }

      lastError = error instanceof Error ? error : new Error(String(error));
    }

    // Don't delay after the last attempt
    if (attempt < MAX_RETRIES) {
      await delay(BASE_BACKOFF_MS * Math.pow(2, attempt));
    }
  }

  throw lastError ?? new Error('Request failed after retries');
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'POST', body: JSON.stringify(body) }),
  put: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'PUT', body: JSON.stringify(body) }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
};
