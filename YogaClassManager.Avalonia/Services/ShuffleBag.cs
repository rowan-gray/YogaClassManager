namespace YogaClassManager.Avalonia.Services;

/// <summary>
///     "Smart shuffle": hands out every item once, in random order, before any repeats - and never the
///     same item twice in a row, even across the boundary between one shuffled round and the next.
///     Plain random picks (what the old MAUI easter egg did) repeat often enough to feel broken.
/// </summary>
public sealed class ShuffleBag<T>
{
    private readonly IReadOnlyList<T> items;
    private readonly Random random;
    private readonly Queue<T> round = new();
    private T? last;
    private bool hasLast;

    public ShuffleBag(IReadOnlyList<T> items, Random? random = null)
    {
        if (items.Count == 0)
            throw new ArgumentException("A shuffle bag needs at least one item.", nameof(items));

        this.items = items;
        this.random = random ?? Random.Shared;
    }

    public T Next()
    {
        if (round.Count == 0)
            Refill();

        last = round.Dequeue();
        hasLast = true;
        return last;
    }

    private void Refill()
    {
        var shuffled = items.ToArray();
        random.Shuffle(shuffled);

        // The new round must not start with what the previous round ended on.
        if (hasLast && shuffled.Length > 1 && EqualityComparer<T>.Default.Equals(shuffled[0], last))
            (shuffled[0], shuffled[^1]) = (shuffled[^1], shuffled[0]);

        foreach (var item in shuffled)
            round.Enqueue(item);
    }
}
