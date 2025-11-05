using NarcoNet.Updater.Infrastructure;
using NarcoNet.Updater.Tests.TestHelpers;

namespace NarcoNet.Updater.Tests.Infrastructure;

public class RetryPolicyTests
{
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new RetryPolicy(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesPolicy()
    {
        // Arrange
        TestLogger logger = new();

        // Act
        RetryPolicy policy = new(logger, 5, TimeSpan.FromSeconds(2));

        // Assert
        policy.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulOperation_CompletesWithoutRetry()
    {
        // Arrange
        TestLogger logger = new();
        RetryPolicy policy = new(logger);
        var executionCount = 0;

        async Task Operation()
        {
            executionCount++;
            await Task.CompletedTask;
        }

        // Act
        await policy.ExecuteAsync(Operation);

        // Assert
        executionCount.Should().Be(1);
        logger.ContainsMessage("Retrying").Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithTransientFailureThenSuccess_RetriesAndSucceeds()
    {
        // Arrange
        TestLogger logger = new();
        RetryPolicy policy = new(logger);
        var executionCount = 0;

        async Task Operation()
        {
            executionCount++;
            if (executionCount < 3)
            {
                throw new IOException("Transient failure");
            }

            await Task.CompletedTask;
        }

        // Act
        await policy.ExecuteAsync(Operation);

        // Assert
        executionCount.Should().Be(3);
        logger.ContainsMessage("Retrying").Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithPermanentFailure_ThrowsAfterMaxAttempts()
    {
        // Arrange
        TestLogger logger = new();
        RetryPolicy policy = new(logger, 3, TimeSpan.FromMilliseconds(10));
        var executionCount = 0;

        Task Operation()
        {
            executionCount++;
            throw new IOException("Permanent failure");
        }

        // Act
        Func<Task> act = async () => await policy.ExecuteAsync(Operation);

        // Assert
        await act.Should().ThrowAsync<IOException>();
        executionCount.Should().Be(4); // Initial + 3 retries
        logger.ContainsMessage("attempt 3/3").Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithNonRetryableException_ThrowsImmediately()
    {
        // Arrange
        TestLogger logger = new();
        RetryPolicy policy = new(logger);
        var executionCount = 0;

        Task Operation()
        {
            executionCount++;
            throw new InvalidOperationException("Non-retryable");
        }

        // Act
        Func<Task> act = async () => await policy.ExecuteAsync(Operation);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        executionCount.Should().Be(1); // No retries
        logger.ContainsMessage("Retrying").Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithRetryableExceptions_RetriesIOException()
    {
        // Arrange
        TestLogger logger = new();
        RetryPolicy policy = new(logger, 2, TimeSpan.FromMilliseconds(10));
        var executionCount = 0;

        async Task Operation()
        {
            executionCount++;
            if (executionCount == 1)
            {
                throw new IOException("IO error");
            }

            await Task.CompletedTask;
        }

        // Act
        await policy.ExecuteAsync(Operation);

        // Assert
        executionCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithRetryableExceptions_RetriesUnauthorizedAccessException()
    {
        // Arrange
        TestLogger logger = new();
        RetryPolicy policy = new(logger, 2, TimeSpan.FromMilliseconds(10));
        var executionCount = 0;

        async Task Operation()
        {
            executionCount++;
            if (executionCount == 1)
            {
                throw new UnauthorizedAccessException("Access denied");
            }

            await Task.CompletedTask;
        }

        // Act
        await policy.ExecuteAsync(Operation);

        // Assert
        executionCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithRetryableExceptions_RetriesTimeoutException()
    {
        // Arrange
        TestLogger logger = new();
        RetryPolicy policy = new(logger, 2, TimeSpan.FromMilliseconds(10));
        var executionCount = 0;

        async Task Operation()
        {
            executionCount++;
            if (executionCount == 1)
            {
                throw new TimeoutException("Timeout");
            }

            await Task.CompletedTask;
        }

        // Act
        await policy.ExecuteAsync(Operation);

        // Assert
        executionCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ThrowsTaskCanceledException()
    {
        // Arrange
        TestLogger logger = new();
        RetryPolicy policy = new(logger, 5, TimeSpan.FromSeconds(10));
        CancellationTokenSource cts = new();
        var executionCount = 0;

        Task Operation()
        {
            executionCount++;
            throw new IOException("Fail");
        }

        // Cancel after first failure
        cts.CancelAfter(100);

        // Act
        Func<Task> act = async () => await policy.ExecuteAsync(Operation, cts.Token);

        // Assert
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsyncGeneric_WithSuccessfulOperation_ReturnsResult()
    {
        // Arrange
        TestLogger logger = new();
        RetryPolicy policy = new(logger);

        async Task<int> Operation()
        {
            await Task.CompletedTask;
            return 42;
        }

        // Act
        int result = await policy.ExecuteAsync(Operation);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsyncGeneric_WithTransientFailure_RetriesAndReturnsResult()
    {
        // Arrange
        TestLogger logger = new();
        RetryPolicy policy = new(logger, 3, TimeSpan.FromMilliseconds(10));
        var executionCount = 0;

        async Task<string> Operation()
        {
            executionCount++;
            if (executionCount < 2)
            {
                throw new IOException("Transient");
            }

            await Task.CompletedTask;
            return "success";
        }

        // Act
        string result = await policy.ExecuteAsync(Operation);

        // Assert
        result.Should().Be("success");
        executionCount.Should().Be(2);
    }

    [Fact]
    public async Task AddRetryableException_AddsCustomExceptionType()
    {
        // Arrange
        TestLogger logger = new();
        RetryPolicy policy = new(logger, 2, TimeSpan.FromMilliseconds(10));
        policy.AddRetryableException<ArgumentException>();

        var executionCount = 0;

        async Task Operation()
        {
            executionCount++;
            if (executionCount == 1)
            {
                throw new ArgumentException("Custom retryable");
            }

            await Task.CompletedTask;
        }

        // Act
        await policy.ExecuteAsync(Operation);

        // Assert
        executionCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ImplementsExponentialBackoff()
    {
        // Arrange
        TestLogger logger = new();
        RetryPolicy policy = new(logger, 3, TimeSpan.FromMilliseconds(100));
        var executionCount = 0;
        List<DateTime> timestamps =
        [
        ];

        async Task Operation()
        {
            timestamps.Add(DateTime.UtcNow);
            executionCount++;
            if (executionCount < 4)
            {
                throw new IOException("Fail");
            }

            await Task.CompletedTask;
        }

        // Act
        await policy.ExecuteAsync(Operation);

        // Assert
        executionCount.Should().Be(4);
        timestamps.Should().HaveCount(4);

        // Verify delays are increasing (with tolerance for jitter)
        double delay1 = (timestamps[1] - timestamps[0]).TotalMilliseconds;
        double delay2 = (timestamps[2] - timestamps[1]).TotalMilliseconds;
        double delay3 = (timestamps[3] - timestamps[2]).TotalMilliseconds;

        delay1.Should().BeInRange(100, 1200); // 100ms base + jitter
        delay2.Should().BeInRange(200, 1300); // 200ms base + jitter
        delay3.Should().BeInRange(400, 1500); // 400ms base + jitter
    }
}
