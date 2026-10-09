namespace Echo_Windows.Core;

/// <summary>Persists full snapshots in order, keeping only the newest snapshot waiting behind an active write.</summary>
public sealed class OrderedPersistenceQueue<T>
{
    private readonly Func<T, Task> persist;
    private readonly object gate = new();
    private T pendingSnapshot = default!;
    private bool hasPendingSnapshot;
    private TaskCompletionSource? pendingBatch;
    private TaskCompletionSource? activeBatch;
    private Task latestTask = Task.CompletedTask;
    private bool running;

    public OrderedPersistenceQueue(Func<T, Task> persist) => this.persist = persist;

    public Task Enqueue(T snapshot)
    {
        bool startPump = false;
        Task queued;
        lock (gate)
        {
            if (pendingBatch is null)
            {
                pendingBatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                latestTask = pendingBatch.Task;
            }
            // Snapshots are complete archive states, so a newer queued one includes all older edits.
            pendingSnapshot = snapshot;
            hasPendingSnapshot = true;
            queued = pendingBatch.Task;
            if (!running) { running = true; startPump = true; }
        }
        if (startPump) _ = PumpAsync();
        return queued;
    }

    public Task FlushAsync()
    {
        lock (gate) return pendingBatch?.Task ?? activeBatch?.Task ?? latestTask;
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            T snapshot;
            TaskCompletionSource batch;
            lock (gate)
            {
                if (!hasPendingSnapshot || pendingBatch is null)
                {
                    running = false;
                    activeBatch = null;
                    return;
                }
                snapshot = pendingSnapshot;
                pendingSnapshot = default!;
                hasPendingSnapshot = false;
                batch = pendingBatch;
                pendingBatch = null;
                activeBatch = batch;
            }

            try
            {
                await persist(snapshot).ConfigureAwait(false);
                batch.TrySetResult();
            }
            catch (Exception error) { batch.TrySetException(error); }
            finally
            {
                lock (gate)
                    if (ReferenceEquals(activeBatch, batch)) activeBatch = null;
            }
        }
    }
}
