using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace DF.Watchable
{
    public interface IReadOnlyWatchable<T> : INotifyPropertyChanged
    {
        T Value { get; }

        event Action<T> Changed;

        Task WaitFor(T value, CancellationToken cancellationToken);

        Task WaitFor(T value, TimeSpan timeout, CancellationToken cancellationToken);

        Task WaitFor(Func<T, bool> predicate, CancellationToken cancellationToken);

        Task WaitFor(Func<T, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken);
    }
}
