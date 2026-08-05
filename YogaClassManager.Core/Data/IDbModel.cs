namespace YogaClassManager.Core.Data;

/// <summary>
///     A query against a single entity type, scoped by <typeparamref name="TFilter" />.
///     Implementations may be backed by an in-memory store (see YogaClassManager.Core.Dummy) or,
///     in the future, a real database - callers never need to know which.
/// </summary>
public interface IDbModel<TModel, TFilter>
{
    TFilter? Filter { get; init; }

    /// <summary>
    ///     Loads a single instance of the TModel matching the filter.
    /// </summary>
    Task<TModel?> LoadSingle(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads multiple instances of the TModel matching the filter.
    /// </summary>
    /// <param name="count">The maximum number of instances to load.</param>
    /// <param name="skip">The number of matching instances to skip over.</param>
    Task<IReadOnlyList<TModel>> LoadMultiple(uint count = uint.MaxValue, uint skip = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Refreshes the provided TModel instance with fresh data from the backing store.
    /// </summary>
    /// <returns>True if the TModel is still backed by data in the store, else false.</returns>
    Task<bool> Refresh(TModel model, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves the current data of the TModel instance into the backing store.
    /// </summary>
    /// <exception cref="ModelExistsException">The TModel already exists in the store when using Create mode.</exception>
    /// <exception cref="ModelDoesNotExistException">The TModel does not exist in the store when using Replace mode.</exception>
    Task Save(TModel model, SaveOptions saveOptions = SaveOptions.CreateOrReplace,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes the provided TModel instance from the backing store.
    /// </summary>
    Task Delete(TModel model, CancellationToken cancellationToken = default);
}

public enum SaveOptions
{
    Create,
    CreateOrReplace,
    Replace
}

public class ModelExistsException : Exception
{
}

public class ModelDoesNotExistException : Exception
{
}
