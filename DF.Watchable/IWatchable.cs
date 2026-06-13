namespace DF.Watchable
{
    public interface IWatchable<T> : IReadOnlyWatchable<T>
    {
        new T Value { get; set; }
    }
}
