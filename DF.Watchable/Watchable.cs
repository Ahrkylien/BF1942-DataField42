using System;
using System.Threading;
using System.Threading.Tasks;

namespace DF.Watchable
{
    public class Watchable<T> : IWatchable<T>
    {
        private T _value;

        public Watchable(T initial) => _value = initial;

        public T Value
        {
            get => _value;
            set
            {
                if (!Equals(_value, value))
                {
                    _value = value;
                    Changed?.Invoke(value);
                }
            }
        }

        public event Action<T> Changed;

        public Task WaitFor(T value, CancellationToken cancellationToken) => WaitForInternal((x) => Equals(x, value), cancellationToken);

        public Task WaitFor(T value, TimeSpan timeout, CancellationToken cancellationToken) => WaitForInternal((x) => Equals(x, value), cancellationToken, timeout);

        public Task WaitFor(Func<T, bool> predicate, CancellationToken cancellationToken) => WaitForInternal(predicate, cancellationToken);

        public Task WaitFor(Func<T, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken) => WaitForInternal(predicate, cancellationToken, timeout);

        private async Task WaitForInternal(Func<T, bool> predicate, CancellationToken cancellationToken, TimeSpan? timeout = null)
        {
            if (predicate(Value))
                return;

            var conditionReachedSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Handler(T val)
            {
                if (predicate(val))
                    conditionReachedSource.TrySetResult(true);
            }

            Changed += Handler;
            Handler(Value);

            using (var timeoutCts = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : null)
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts?.Token ?? CancellationToken.None))
            using (linkedCts.Token.Register(() => conditionReachedSource.TrySetCanceled()))
            {
                try
                {
                    await conditionReachedSource.Task.ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    if (timeoutCts?.IsCancellationRequested == true)
                        throw new TimeoutException($"Condition not reached within {timeout.Value.TotalMilliseconds} ms.");
                    throw;
                }
                finally
                {
                    Changed -= Handler;
                }
            }
        }
    }

}
