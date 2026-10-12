namespace Echo_Windows.Core;

public enum PriorityWorkQueueInsertResult
{
    Added,
    Duplicate,
    Full
}

public sealed class PriorityWorkQueue<T>(int capacity) where T : class
{
    private sealed record QueuedItem(T Value, bool Priority);
    private readonly LinkedList<QueuedItem> pending = [];
    public int Count => pending.Count;
    public int Capacity { get; } = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));

    public PriorityWorkQueueInsertResult Enqueue(T item, bool priority, Func<T, bool> isDuplicate)
    {
        if (pending.Any(queued => isDuplicate(queued.Value))) return PriorityWorkQueueInsertResult.Duplicate;
        if (pending.Count >= Capacity) return PriorityWorkQueueInsertResult.Full;
        var queued = new QueuedItem(item, priority);
        if (priority) pending.AddFirst(queued);
        else pending.AddLast(queued);
        return PriorityWorkQueueInsertResult.Added;
    }

    public T? Find(Func<T, bool> predicate) => pending.FirstOrDefault(queued => predicate(queued.Value))?.Value;

    public bool Promote(Func<T, bool> predicate)
    {
        var node = pending.First;
        while (node is not null)
        {
            if (predicate(node.Value.Value))
            {
                if (node.Value.Priority) return false;
                T item = node.Value.Value;
                pending.Remove(node);
                pending.AddFirst(new QueuedItem(item, true));
                return true;
            }
            node = node.Next;
        }
        return false;
    }

    public T? Dequeue()
    {
        if (pending.First is not { } first) return null;
        pending.RemoveFirst();
        return first.Value.Value;
    }

    public List<T> Clear()
    {
        var removed = pending.Select(queued => queued.Value).ToList();
        pending.Clear();
        return removed;
    }
}
