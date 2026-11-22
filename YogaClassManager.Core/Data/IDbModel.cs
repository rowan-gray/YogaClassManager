namespace YogaClassManager.Core.Data;

/// <summary>
///     A read-only query against a single entity type, scoped by <typeparamref name="TFilter" />.
///     Implementations may be backed by an in-memory store (see YogaClassManager.Core.Dummy) or a
///     SQLite database (see YogaClassManager.Core.SQLite) - callers never need to know which. Writes
///     go through the entity's repository, so this is what repositories hand out from Query.
/// </summary>
public interface IReadOnlyDbModel<TModel, TFilter>
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
}

/// <summary>
///     A query that can also write its models back to the backing store.
/// </summary>
public interface IDbModel<TModel, TFilter> : IReadOnlyDbModel<TModel, TFilter>
{
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
