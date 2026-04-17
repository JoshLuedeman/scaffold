import { ApiError } from '../api';

// We test the `request` function indirectly via the `api` helper by
// calling `api.get`, `api.post`, etc.  Because the module grabs `fetch`
// from the global scope and also uses an internal MSAL instance we mock
// both at the module boundary.

// --- helpers ---------------------------------------------------------------

let fetchMock: ReturnType<typeof vi.fn>;

beforeEach(() => {
  fetchMock = vi.fn();
  vi.stubGlobal('fetch', fetchMock);
  vi.useFakeTimers({ shouldAdvanceTime: true });
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

// Fresh import every test so module-level state (msalInstance) resets.
async function getApi() {
  // Bust vitest module cache so each test starts clean
  vi.resetModules();
  const mod = await import('../api');
  return mod;
}

function jsonResponse(status: number, body: unknown, contentType = 'application/json') {
  return new Response(JSON.stringify(body), {
    status,
    statusText: status === 200 ? 'OK' : 'Error',
    headers: { 'content-type': contentType },
  });
}

function problemResponse(status: number, body: { title: string; detail: string; instance?: string }) {
  return new Response(JSON.stringify({ status, ...body }), {
    status,
    statusText: 'Error',
    headers: { 'content-type': 'application/problem+json' },
  });
}

// ---------------------------------------------------------------------------

describe('ApiError', () => {
  it('creates an error with all fields', () => {
    const error = new ApiError(404, 'Not Found', 'The resource was not found', '/api/projects/123');

    expect(error).toBeInstanceOf(Error);
    expect(error).toBeInstanceOf(ApiError);
    expect(error.name).toBe('ApiError');
    expect(error.status).toBe(404);
    expect(error.title).toBe('Not Found');
    expect(error.detail).toBe('The resource was not found');
    expect(error.instance).toBe('/api/projects/123');
    expect(error.message).toBe('The resource was not found');
  });

  it('uses title as message when detail is empty', () => {
    const error = new ApiError(500, 'Internal Server Error', '');
    expect(error.message).toBe('Internal Server Error');
  });

  it('sets instance to undefined when not provided', () => {
    const error = new ApiError(400, 'Bad Request', 'Invalid input');
    expect(error.instance).toBeUndefined();
  });

  it('has the correct prototype chain', () => {
    const error = new ApiError(422, 'Validation Error', 'Invalid field');
    expect(error instanceof Error).toBe(true);
    expect(error instanceof ApiError).toBe(true);
  });
});

describe('api.get / request function', () => {
  it('makes a GET request and returns JSON', async () => {
    const { api } = await getApi();
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { id: '1', name: 'Project' }));

    const result = await api.get<{ id: string; name: string }>('/projects/1');

    expect(result).toEqual({ id: '1', name: 'Project' });
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/projects/1'),
      expect.objectContaining({ headers: expect.objectContaining({ 'Content-Type': 'application/json' }) }),
    );
  });

  it('makes a POST request with body', async () => {
    const { api } = await getApi();
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { ok: true }));

    await api.post('/projects', { name: 'New' });

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/projects'),
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ name: 'New' }),
      }),
    );
  });

  it('makes a PUT request with body', async () => {
    const { api } = await getApi();
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { ok: true }));

    await api.put('/projects/1', { name: 'Updated' });

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/projects/1'),
      expect.objectContaining({
        method: 'PUT',
        body: JSON.stringify({ name: 'Updated' }),
      }),
    );
  });

  it('makes a DELETE request', async () => {
    const { api } = await getApi();
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { ok: true }));

    await api.delete('/projects/1');

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/projects/1'),
      expect.objectContaining({ method: 'DELETE' }),
    );
  });
});

describe('retry logic', () => {
  it('retries on 500 errors up to 3 times then throws', async () => {
    const { api, ApiError: AE } = await getApi();
    fetchMock.mockResolvedValue(jsonResponse(500, { title: 'Server Error', detail: 'Boom' }));

    const promise = api.get('/fail');
    // Attach no-op catch immediately to prevent unhandled rejection tracking
    const safePromise = promise.catch(() => {});

    // Advance through 3 retry delays: 1s, 2s, 4s
    await vi.advanceTimersByTimeAsync(1000);
    await vi.advanceTimersByTimeAsync(2000);
    await vi.advanceTimersByTimeAsync(4000);
    await safePromise;

    try {
      await promise;
      expect.fail('should have thrown');
    } catch (err) {
      expect(err).toBeInstanceOf(AE);
    }

    // 1 initial + 3 retries = 4 total fetches
    expect(fetchMock).toHaveBeenCalledTimes(4);
  });

  it('does NOT retry on 4xx errors', async () => {
    const { api, ApiError: AE } = await getApi();
    fetchMock.mockResolvedValueOnce(
      problemResponse(422, { title: 'Validation', detail: 'Bad field' }),
    );

    await expect(api.get('/bad')).rejects.toBeInstanceOf(AE);

    // Only 1 attempt — no retries
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('does NOT retry on 404 errors', async () => {
    const { api } = await getApi();
    fetchMock.mockResolvedValueOnce(jsonResponse(404, { title: 'Not Found', detail: 'Missing' }));

    await expect(api.get('/missing')).rejects.toThrow();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('succeeds on retry after transient 500', async () => {
    const { api } = await getApi();
    fetchMock
      .mockResolvedValueOnce(jsonResponse(503, { title: 'Unavailable', detail: '' }))
      .mockResolvedValueOnce(jsonResponse(200, { recovered: true }));

    const promise = api.get<{ recovered: boolean }>('/recover');

    // Advance past first backoff (1s)
    await vi.advanceTimersByTimeAsync(1000);

    const result = await promise;
    expect(result).toEqual({ recovered: true });
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('retries network errors (fetch throws)', async () => {
    const { api } = await getApi();
    let callCount = 0;
    fetchMock.mockImplementation(async () => {
      callCount++;
      if (callCount <= 2) throw new TypeError('Failed to fetch');
      return jsonResponse(200, { ok: true });
    });

    const promise = api.get<{ ok: boolean }>('/flaky');

    await vi.advanceTimersByTimeAsync(1000); // 1st retry
    await vi.advanceTimersByTimeAsync(2000); // 2nd retry

    const result = await promise;
    expect(result).toEqual({ ok: true });
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it('throws last network error after exhausting retries', async () => {
    const { api } = await getApi();
    fetchMock.mockImplementation(async () => { throw new TypeError('Failed to fetch'); });

    const promise = api.get('/down');
    // Attach no-op catch immediately to prevent unhandled rejection tracking
    const safePromise = promise.catch(() => {});

    await vi.advanceTimersByTimeAsync(1000);
    await vi.advanceTimersByTimeAsync(2000);
    await vi.advanceTimersByTimeAsync(4000);
    await safePromise;

    try {
      await promise;
      expect.fail('should have thrown');
    } catch (err) {
      expect(err).toBeInstanceOf(TypeError);
      expect((err as TypeError).message).toBe('Failed to fetch');
    }
    expect(fetchMock).toHaveBeenCalledTimes(4);
  });
});

describe('error parsing', () => {
  it('parses RFC 7807 ProblemDetails from application/problem+json', async () => {
    const { api, ApiError: AE } = await getApi();
    fetchMock.mockResolvedValueOnce(
      problemResponse(400, { title: 'Bad Request', detail: 'Name is required', instance: '/api/projects' }),
    );

    try {
      await api.get('/bad');
      expect.fail('should have thrown');
    } catch (err) {
      expect(err).toBeInstanceOf(AE);
      const apiErr = err as InstanceType<typeof AE>;
      expect(apiErr.status).toBe(400);
      expect(apiErr.title).toBe('Bad Request');
      expect(apiErr.detail).toBe('Name is required');
      expect(apiErr.instance).toBe('/api/projects');
    }
  });

  it('parses error from regular JSON response', async () => {
    const { api, ApiError: AE } = await getApi();
    fetchMock.mockResolvedValueOnce(
      jsonResponse(403, { title: 'Forbidden', detail: 'Access denied' }),
    );

    try {
      await api.get('/forbidden');
      expect.fail('should have thrown');
    } catch (err) {
      expect(err).toBeInstanceOf(AE);
      const apiErr = err as InstanceType<typeof AE>;
      expect(apiErr.status).toBe(403);
      expect(apiErr.title).toBe('Forbidden');
      expect(apiErr.detail).toBe('Access denied');
    }
  });

  it('creates fallback error when response body is not JSON', async () => {
    const { api, ApiError: AE } = await getApi();
    fetchMock.mockResolvedValueOnce(
      new Response('Internal Error', {
        status: 400,
        statusText: 'Bad Request',
        headers: { 'content-type': 'text/plain' },
      }),
    );

    try {
      await api.get('/text-error');
      expect.fail('should have thrown');
    } catch (err) {
      expect(err).toBeInstanceOf(AE);
      const apiErr = err as InstanceType<typeof AE>;
      expect(apiErr.status).toBe(400);
      expect(apiErr.title).toBe('Bad Request');
      expect(apiErr.detail).toContain('400');
    }
  });
});