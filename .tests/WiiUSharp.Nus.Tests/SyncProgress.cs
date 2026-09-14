namespace WiiUSharp.Nus.Tests;

/// <summary>
/// Reports on the calling thread so tests see every message in order.
/// </summary>
internal sealed class SyncProgress<T> : IProgress<T>
{
    private readonly Action<T> _report;

    public SyncProgress(Action<T> report)
    {
        _report = report;
    }

    public void Report(T value) => _report(value);
}
