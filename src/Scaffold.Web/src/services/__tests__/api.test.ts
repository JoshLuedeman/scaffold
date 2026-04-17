import { ApiError } from '../api';

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
