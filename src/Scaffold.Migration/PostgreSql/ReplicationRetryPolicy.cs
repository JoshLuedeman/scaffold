using Npgsql;

namespace Scaffold.Migration.PostgreSql;

/// <summary>
/// Retry policy with exponential backoff and circuit breaker for PostgreSQL operations.
/// Handles transient NpgsqlException errors (connection loss, timeout, deadlock, etc).
/// </summary>
public class ReplicationRetryPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maxDelay;
    private readonly int _circuitBreakerThreshold;

    private int _consecutiveFailures;
    private DateTime _circuitOpenUntil = DateTime.MinValue;

    public ReplicationRetryPolicy(
        int maxRetries = 5,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null,
        int circuitBreakerThreshold = 10)
    {
        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        _maxDelay = maxDelay ?? TimeSpan.FromSeconds(60);
        _circuitBreakerThreshold = circuitBreakerThreshold;
    }

    public int ConsecutiveFailures => _consecutiveFailures;
    public bool IsCircuitOpen => DateTime.UtcNow < _circuitOpenUntil;
    public int MaxRetries => _maxRetries;
    public TimeSpan InitialDelay => _initialDelay;
    public TimeSpan MaxDelay => _maxDelay;
    public int CircuitBreakerThreshold => _circuitBreakerThreshold;

    /// <summary>
    /// Executes an async operation with retry and circuit breaker logic.
    /// Retries only on transient NpgsqlException errors with exponential backoff.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct = default)
    {
        if (operation is null) throw new ArgumentNullException(nameof(operation));

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            if (IsCircuitOpen)
            {
                throw new CircuitBreakerOpenException(
                    $"Circuit breaker is open after {_consecutiveFailures} consecutive failures. " +
                    $"Will retry after {_circuitOpenUntil:O}.");
            }

            try
            {
                var result = await operation(ct);
                RecordSuccess();
                return result;
            }
            catch (NpgsqlException ex) when (IsTransient(ex) && attempt < _maxRetries)
            {
                RecordFailure();
                var delay = CalculateDelay(attempt);
                await Task.Delay(delay, ct);
            }
            catch (NpgsqlException ex) when (IsTransient(ex) && attempt == _maxRetries)
            {
                RecordFailure();
                throw;
            }
        }

        throw new InvalidOperationException("Retry loop exited unexpectedly.");
    }

    /// <summary>
    /// Executes a void async operation with retry logic.
    /// </summary>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken ct = default)
    {
        if (operation is null) throw new ArgumentNullException(nameof(operation));

        await ExecuteAsync(async token =>
        {
            await operation(token);
            return true; // dummy return
        }, ct);
    }

    /// <summary>
    /// Determines if an NpgsqlException is transient and worth retrying.
    /// Covers connection failures, deadlocks, resource limits, and server restarts.
    /// </summary>
    public static bool IsTransient(NpgsqlException ex)
    {
        // Connection-related inner exceptions
        if (ex.InnerException is System.IO.IOException) return true;
        if (ex.InnerException is System.Net.Sockets.SocketException) return true;

        // PostgreSQL transient error codes
        if (ex is PostgresException pgEx)
        {
            return pgEx.SqlState switch
            {
                "08000" => true, // connection_exception
                "08001" => true, // sqlclient_unable_to_establish_sqlconnection
                "08003" => true, // connection_does_not_exist
                "08006" => true, // connection_failure
                "40001" => true, // serialization_failure
                "40P01" => true, // deadlock_detected
                "53000" => true, // insufficient_resources
                "53100" => true, // disk_full
                "53200" => true, // out_of_memory
                "53300" => true, // too_many_connections
                "57P01" => true, // admin_shutdown
                "57P02" => true, // crash_shutdown
                "57P03" => true, // cannot_connect_now
                _ => false
            };
        }

        return false;
    }

    /// <summary>
    /// Calculates delay with exponential backoff and jitter, capped at MaxDelay.
    /// </summary>
    internal TimeSpan CalculateDelay(int attempt)
    {
        var baseDelay = _initialDelay.TotalMilliseconds * Math.Pow(2, attempt);
        var jitter = Random.Shared.NextDouble() * 0.3 * baseDelay; // up to +30% jitter
        var totalMs = Math.Min(baseDelay + jitter, _maxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(totalMs);
    }

    /// <summary>
    /// Records a successful operation, resetting consecutive failure count and circuit breaker.
    /// </summary>
    internal void RecordSuccess()
    {
        Interlocked.Exchange(ref _consecutiveFailures, 0);
        _circuitOpenUntil = DateTime.MinValue;
    }

    /// <summary>
    /// Records a failed operation. Opens the circuit breaker if threshold is reached.
    /// </summary>
    internal void RecordFailure()
    {
        var failures = Interlocked.Increment(ref _consecutiveFailures);
        if (failures >= _circuitBreakerThreshold)
        {
            // Open circuit for exponential backoff based on failure count
            var openDuration = TimeSpan.FromSeconds(
                Math.Min(300, Math.Pow(2, failures - _circuitBreakerThreshold)));
            _circuitOpenUntil = DateTime.UtcNow + openDuration;
        }
    }
}

/// <summary>
/// Thrown when the circuit breaker is open and operations should not be attempted.
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException() { }
    public CircuitBreakerOpenException(string message) : base(message) { }
    public CircuitBreakerOpenException(string message, Exception inner) : base(message, inner) { }
}
