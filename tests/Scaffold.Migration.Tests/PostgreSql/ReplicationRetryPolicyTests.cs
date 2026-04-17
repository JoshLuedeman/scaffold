using Npgsql;
using Scaffold.Migration.PostgreSql;

namespace Scaffold.Migration.Tests.PostgreSql;

public class ReplicationRetryPolicyTests
{
    #region Constructor and defaults

    [Fact]
    public void Constructor_DefaultValues_AreReasonable()
    {
        var policy = new ReplicationRetryPolicy();

        Assert.Equal(5, policy.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(1), policy.InitialDelay);
        Assert.Equal(TimeSpan.FromSeconds(60), policy.MaxDelay);
        Assert.Equal(10, policy.CircuitBreakerThreshold);
        Assert.Equal(0, policy.ConsecutiveFailures);
        Assert.False(policy.IsCircuitOpen);
    }

    [Fact]
    public void Constructor_CustomValues_ArePreserved()
    {
        var policy = new ReplicationRetryPolicy(
            maxRetries: 3,
            initialDelay: TimeSpan.FromMilliseconds(500),
            maxDelay: TimeSpan.FromSeconds(30),
            circuitBreakerThreshold: 5);

        Assert.Equal(3, policy.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(500), policy.InitialDelay);
        Assert.Equal(TimeSpan.FromSeconds(30), policy.MaxDelay);
        Assert.Equal(5, policy.CircuitBreakerThreshold);
    }

    #endregion

    #region CalculateDelay

    [Fact]
    public void CalculateDelay_Attempt0_IsAroundInitialDelay()
    {
        var policy = new ReplicationRetryPolicy(initialDelay: TimeSpan.FromSeconds(1));

        var delay = policy.CalculateDelay(0);

        // Base = 1s * 2^0 = 1s, plus up to 30% jitter = 1.0–1.3s
        Assert.InRange(delay.TotalMilliseconds, 1000, 1300);
    }

    [Fact]
    public void CalculateDelay_Attempt1_IsAroundDouble()
    {
        var policy = new ReplicationRetryPolicy(initialDelay: TimeSpan.FromSeconds(1));

        var delay = policy.CalculateDelay(1);

        // Base = 1s * 2^1 = 2s, plus up to 30% jitter = 2.0–2.6s
        Assert.InRange(delay.TotalMilliseconds, 2000, 2600);
    }

    [Fact]
    public void CalculateDelay_Attempt4_IsAroundSixteenSeconds()
    {
        var policy = new ReplicationRetryPolicy(initialDelay: TimeSpan.FromSeconds(1));

        var delay = policy.CalculateDelay(4);

        // Base = 1s * 2^4 = 16s, plus up to 30% jitter = 16–20.8s
        Assert.InRange(delay.TotalMilliseconds, 16000, 20800);
    }

    [Fact]
    public void CalculateDelay_ExceedingMaxDelay_IsCapped()
    {
        var policy = new ReplicationRetryPolicy(
            initialDelay: TimeSpan.FromSeconds(10),
            maxDelay: TimeSpan.FromSeconds(30));

        var delay = policy.CalculateDelay(5);

        // Base = 10s * 2^5 = 320s, but capped at 30s
        Assert.True(delay.TotalSeconds <= 30,
            $"Delay {delay.TotalSeconds:F1}s exceeds maxDelay of 30s");
    }

    [Fact]
    public void CalculateDelay_MultipleAttempts_Grows()
    {
        var policy = new ReplicationRetryPolicy(
            initialDelay: TimeSpan.FromSeconds(1),
            maxDelay: TimeSpan.FromSeconds(120));

        // Run many samples to verify trend (exponential growth)
        var delay0 = Enumerable.Range(0, 100)
            .Select(_ => policy.CalculateDelay(0).TotalMilliseconds).Average();
        var delay2 = Enumerable.Range(0, 100)
            .Select(_ => policy.CalculateDelay(2).TotalMilliseconds).Average();

        Assert.True(delay2 > delay0, "Delay should grow with attempt number");
    }

    #endregion

    #region IsTransient

    [Theory]
    [InlineData("08000")] // connection_exception
    [InlineData("08001")] // sqlclient_unable_to_establish_sqlconnection
    [InlineData("08003")] // connection_does_not_exist
    [InlineData("08006")] // connection_failure
    [InlineData("40001")] // serialization_failure
    [InlineData("40P01")] // deadlock_detected
    [InlineData("53000")] // insufficient_resources
    [InlineData("53100")] // disk_full
    [InlineData("53200")] // out_of_memory
    [InlineData("53300")] // too_many_connections
    [InlineData("57P01")] // admin_shutdown
    [InlineData("57P02")] // crash_shutdown
    [InlineData("57P03")] // cannot_connect_now
    public void IsTransient_TransientSqlStates_ReturnsTrue(string sqlState)
    {
        var ex = CreatePostgresException(sqlState);

        Assert.True(ReplicationRetryPolicy.IsTransient(ex));
    }

    [Theory]
    [InlineData("23505")] // unique_violation
    [InlineData("42P01")] // undefined_table
    [InlineData("42601")] // syntax_error
    [InlineData("42501")] // insufficient_privilege
    [InlineData("23503")] // foreign_key_violation
    public void IsTransient_NonTransientSqlStates_ReturnsFalse(string sqlState)
    {
        var ex = CreatePostgresException(sqlState);

        Assert.False(ReplicationRetryPolicy.IsTransient(ex));
    }

    [Fact]
    public void IsTransient_IOException_ReturnsTrue()
    {
        var ex = new NpgsqlException("Connection lost", new System.IO.IOException("pipe broken"));

        Assert.True(ReplicationRetryPolicy.IsTransient(ex));
    }

    [Fact]
    public void IsTransient_SocketException_ReturnsTrue()
    {
        var ex = new NpgsqlException("Connection lost",
            new System.Net.Sockets.SocketException(10054));

        Assert.True(ReplicationRetryPolicy.IsTransient(ex));
    }

    [Fact]
    public void IsTransient_NpgsqlExceptionWithNoInnerOrSqlState_ReturnsFalse()
    {
        var ex = new NpgsqlException("Generic error");

        Assert.False(ReplicationRetryPolicy.IsTransient(ex));
    }

    #endregion

    #region ExecuteAsync<T>

    [Fact]
    public async Task ExecuteAsync_SucceedsFirstTry_ReturnsResult()
    {
        var policy = new ReplicationRetryPolicy();
        int callCount = 0;

        var result = await policy.ExecuteAsync(ct =>
        {
            callCount++;
            return Task.FromResult(42);
        });

        Assert.Equal(42, result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task ExecuteAsync_SucceedsFirstTry_ResetsFailures()
    {
        var policy = new ReplicationRetryPolicy();
        // Pre-record some failures
        policy.RecordFailure();
        policy.RecordFailure();
        Assert.Equal(2, policy.ConsecutiveFailures);

        await policy.ExecuteAsync(ct => Task.FromResult(true));

        Assert.Equal(0, policy.ConsecutiveFailures);
    }

    [Fact]
    public async Task ExecuteAsync_TransientFailureThenSuccess_ReturnsResult()
    {
        var policy = new ReplicationRetryPolicy(
            maxRetries: 3,
            initialDelay: TimeSpan.FromMilliseconds(1),
            maxDelay: TimeSpan.FromMilliseconds(10));

        int callCount = 0;

        var result = await policy.ExecuteAsync(ct =>
        {
            callCount++;
            if (callCount < 3)
            {
                throw new NpgsqlException("transient",
                    new System.IO.IOException("connection reset"));
            }
            return Task.FromResult("success");
        });

        Assert.Equal("success", result);
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task ExecuteAsync_ExhaustsRetries_Throws()
    {
        var policy = new ReplicationRetryPolicy(
            maxRetries: 2,
            initialDelay: TimeSpan.FromMilliseconds(1),
            maxDelay: TimeSpan.FromMilliseconds(10));

        int callCount = 0;

        await Assert.ThrowsAsync<NpgsqlException>(() =>
            policy.ExecuteAsync<int>(ct =>
            {
                callCount++;
                throw new NpgsqlException("transient",
                    new System.IO.IOException("always fails"));
            }));

        // maxRetries=2 means 3 attempts (0, 1, 2)
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task ExecuteAsync_NonTransientException_DoesNotRetry()
    {
        var policy = new ReplicationRetryPolicy(
            maxRetries: 5,
            initialDelay: TimeSpan.FromMilliseconds(1));

        int callCount = 0;

        await Assert.ThrowsAsync<NpgsqlException>(() =>
            policy.ExecuteAsync<int>(ct =>
            {
                callCount++;
                throw new NpgsqlException("non-transient error");
            }));

        // Non-transient: not caught by the 'when' clause, so it propagates immediately
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task ExecuteAsync_NullOperation_ThrowsArgumentNullException()
    {
        var policy = new ReplicationRetryPolicy();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            policy.ExecuteAsync<int>(null!));
    }

    #endregion

    #region ExecuteAsync (void overload)

    [Fact]
    public async Task ExecuteAsyncVoid_SucceedsFirstTry_Completes()
    {
        var policy = new ReplicationRetryPolicy();
        int callCount = 0;

        await policy.ExecuteAsync(ct =>
        {
            callCount++;
            return Task.CompletedTask;
        });

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task ExecuteAsyncVoid_TransientFailureThenSuccess_Completes()
    {
        var policy = new ReplicationRetryPolicy(
            maxRetries: 3,
            initialDelay: TimeSpan.FromMilliseconds(1),
            maxDelay: TimeSpan.FromMilliseconds(10));

        int callCount = 0;

        await policy.ExecuteAsync(ct =>
        {
            callCount++;
            if (callCount < 2)
            {
                throw new NpgsqlException("transient",
                    new System.IO.IOException("connection reset"));
            }
            return Task.CompletedTask;
        });

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task ExecuteAsyncVoid_NullOperation_ThrowsArgumentNullException()
    {
        var policy = new ReplicationRetryPolicy();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            policy.ExecuteAsync((Func<CancellationToken, Task>)null!));
    }

    #endregion

    #region RecordSuccess / RecordFailure

    [Fact]
    public void RecordSuccess_ResetsConsecutiveFailures()
    {
        var policy = new ReplicationRetryPolicy();
        policy.RecordFailure();
        policy.RecordFailure();
        policy.RecordFailure();
        Assert.Equal(3, policy.ConsecutiveFailures);

        policy.RecordSuccess();

        Assert.Equal(0, policy.ConsecutiveFailures);
    }

    [Fact]
    public void RecordFailure_IncrementsConsecutiveFailures()
    {
        var policy = new ReplicationRetryPolicy();

        policy.RecordFailure();
        Assert.Equal(1, policy.ConsecutiveFailures);

        policy.RecordFailure();
        Assert.Equal(2, policy.ConsecutiveFailures);

        policy.RecordFailure();
        Assert.Equal(3, policy.ConsecutiveFailures);
    }

    #endregion

    #region Circuit breaker

    [Fact]
    public void CircuitBreaker_OpensAfterThresholdFailures()
    {
        var policy = new ReplicationRetryPolicy(circuitBreakerThreshold: 3);

        policy.RecordFailure(); // 1
        policy.RecordFailure(); // 2
        Assert.False(policy.IsCircuitOpen);

        policy.RecordFailure(); // 3 — reaches threshold
        Assert.True(policy.IsCircuitOpen);
    }

    [Fact]
    public async Task CircuitBreaker_PreventsExecution_WhenOpen()
    {
        var policy = new ReplicationRetryPolicy(circuitBreakerThreshold: 1);
        policy.RecordFailure(); // Opens circuit

        var ex = await Assert.ThrowsAsync<CircuitBreakerOpenException>(() =>
            policy.ExecuteAsync(ct => Task.FromResult(42)));
        Assert.Contains("Circuit breaker is open", ex.Message);
    }

    [Fact]
    public void CircuitBreaker_ResetsAfterSuccess()
    {
        var policy = new ReplicationRetryPolicy(circuitBreakerThreshold: 2);

        policy.RecordFailure();
        policy.RecordFailure();
        Assert.True(policy.IsCircuitOpen);

        policy.RecordSuccess();

        Assert.False(policy.IsCircuitOpen);
        Assert.Equal(0, policy.ConsecutiveFailures);
    }

    #endregion

    #region CircuitBreakerOpenException

    [Fact]
    public void CircuitBreakerOpenException_ContainsUsefulMessage()
    {
        var ex = new CircuitBreakerOpenException("test message");

        Assert.Equal("test message", ex.Message);
    }

    [Fact]
    public void CircuitBreakerOpenException_DefaultConstructor_Works()
    {
        var ex = new CircuitBreakerOpenException();

        Assert.NotNull(ex);
    }

    [Fact]
    public void CircuitBreakerOpenException_WithInnerException_Works()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new CircuitBreakerOpenException("outer", inner);

        Assert.Equal("outer", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task ExecuteAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var policy = new ReplicationRetryPolicy();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            policy.ExecuteAsync(ct => Task.FromResult(42), cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_CancelledDuringRetryDelay_ThrowsOperationCanceledException()
    {
        var policy = new ReplicationRetryPolicy(
            maxRetries: 5,
            initialDelay: TimeSpan.FromSeconds(30)); // long delay

        using var cts = new CancellationTokenSource();
        int callCount = 0;

        var task = policy.ExecuteAsync<int>(ct =>
        {
            callCount++;
            throw new NpgsqlException("transient",
                new System.IO.IOException("connection reset"));
        }, cts.Token);

        // Cancel shortly after the first failure triggers a retry delay
        await Task.Delay(50);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    #endregion

    #region Helpers

    private static PostgresException CreatePostgresException(string sqlState)
    {
        // PostgresException requires specific construction; use reflection or helper
        // The simplest way is to use the PostgresException constructor that's available
        return new PostgresException(
            messageText: $"Error with state {sqlState}",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: sqlState);
    }

    #endregion
}
