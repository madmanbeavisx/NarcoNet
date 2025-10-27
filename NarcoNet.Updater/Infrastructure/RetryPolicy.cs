using NarcoNet.Updater.Interfaces;

namespace NarcoNet.Updater.Infrastructure;

/// <summary>
///   Provides resilient retry logic for transient failures.
///   Implements the Retry pattern with exponential backoff.
/// </summary>
public class RetryPolicy
{
  private readonly TimeSpan _initialDelay;
  private readonly ILogger _logger;
  private readonly int _maxRetryAttempts;
  private readonly ICollection<Type> _retryableExceptions;

  /// <summary>
  ///   Initializes a new instance of the <see cref="RetryPolicy" /> class.
  /// </summary>
  /// <param name="logger">The logger for retry operations.</param>
  /// <param name="maxRetryAttempts">Maximum number of retry attempts.</param>
  /// <param name="initialDelay">Initial delay between retries.</param>
  public RetryPolicy(ILogger logger, int maxRetryAttempts = 3, TimeSpan? initialDelay = null)
  {
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _maxRetryAttempts = maxRetryAttempts;
    _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
    _retryableExceptions = new List<Type>
    {
      typeof(IOException),
      typeof(UnauthorizedAccessException),
      typeof(TimeoutException)
    };
  }

  /// <summary>
  ///   Executes an action with retry logic.
  /// </summary>
  /// <param name="action">The action to execute.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>A task representing the operation.</returns>
  public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
  {
    int attempt = 0;
    while (true)
      try
      {
        await action();
        return;
      }
      catch (Exception ex) when (IsRetryable(ex) && attempt < _maxRetryAttempts)
      {
        attempt++;
        TimeSpan delay = CalculateDelay(attempt);

        _logger.LogWarning(
          $"⚠️ Mission compromised (attempt {attempt}/{_maxRetryAttempts}). Regrouping in {delay.TotalSeconds}s... Problem: {ex.Message}");

        await Task.Delay(delay, cancellationToken);
      }
  }

  /// <summary>
  ///   Executes a function with retry logic.
  /// </summary>
  /// <typeparam name="T">The return type.</typeparam>
  /// <param name="func">The function to execute.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>The result of the function.</returns>
  public async Task<T> ExecuteAsync<T>(Func<Task<T>> func, CancellationToken cancellationToken = default)
  {
    int attempt = 0;
    while (true)
      try
      {
        return await func();
      }
      catch (Exception ex) when (IsRetryable(ex) && attempt < _maxRetryAttempts)
      {
        attempt++;
        TimeSpan delay = CalculateDelay(attempt);

        _logger.LogWarning(
          $"⚠️ Mission compromised (attempt {attempt}/{_maxRetryAttempts}). Regrouping in {delay.TotalSeconds}s... Problem: {ex.Message}");

        await Task.Delay(delay, cancellationToken);
      }
  }

  /// <summary>
  ///   Determines if an exception is retryable.
  /// </summary>
  private bool IsRetryable(Exception exception)
  {
    foreach (Type retryableType in _retryableExceptions)
      if (retryableType.IsInstanceOfType(exception))
        return true;
    return false;
  }

  /// <summary>
  ///   Calculates the delay for a given attempt using exponential backoff.
  /// </summary>
  private TimeSpan CalculateDelay(int attempt)
  {
    // Exponential backoff: 1s, 2s, 4s, 8s, etc.
    TimeSpan exponentialDelay = TimeSpan.FromMilliseconds(_initialDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));

    // Add jitter to prevent thundering herd
    TimeSpan jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));

    return exponentialDelay + jitter;
  }

  /// <summary>
  ///   Adds a custom exception type to the retryable list.
  /// </summary>
  public void AddRetryableException<TException>() where TException : Exception
  {
    _retryableExceptions.Add(typeof(TException));
  }
}
