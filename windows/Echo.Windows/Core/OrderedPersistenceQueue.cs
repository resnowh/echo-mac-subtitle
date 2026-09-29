namespace Echo_Windows.Core;

public sealed class OrderedPersistenceQueue<T>(Func<T, Task> persist)
{
    private Task tail = Task.CompletedTask;

    public Task Enqueue(T snapshot)
    {
        Task previous = tail;
        tail = PersistAfterAsync(previous, snapshot);
        return tail;
    }

    public Task FlushAsync() => tail;

    private async Task PersistAfterAsync(Task previous, T snapshot)
    {
        try { await previous; }
        catch { /* A failed checkpoint must not block a newer full snapshot. */ }
        await persist(snapshot);
    }
}
