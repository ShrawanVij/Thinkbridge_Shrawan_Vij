namespace QuotesApi.Caching;

// Instrumentation for the load test: counts how many times the underlying
// "database" fetch actually ran, independent of how many HTTP requests came
// in. Under stampede protection, N concurrent cache misses on the same key
// should still only increment this once.
public class DbHitCounter
{
    private long _count;

    public void Increment() => Interlocked.Increment(ref _count);

    public long Count => Interlocked.Read(ref _count);

    public void Reset() => Interlocked.Exchange(ref _count, 0);
}
