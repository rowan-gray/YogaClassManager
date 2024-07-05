namespace YogaClassManager.NewDatabase;

public interface IDbModel<TModel, TFilter>
{
    public TFilter? Filter { get; init; }
        
    /// <summary>
    ///     Loads a single instance of the TModel from the database.
    /// </summary>
    /// <returns>
    ///     The model matching the provided filter. 
    /// </returns>
    public Task<TModel?> LoadSingle(CancellationToken cancellationToken = new());

    /// <summary>
    ///     Loads multiple instances of the TModel from the database.
    /// </summary>
    /// <param name="count">The number of instances to load.</param>
    /// <param name="skip">The number of instances to skip over.</param>
    /// <returns>
    ///     A list of models matching the provided filter.
    /// </returns>
    public Task<IEnumerable<TModel>> LoadMultiple(uint count = uint.MaxValue, uint skip = 0, CancellationToken cancellationToken = new());

    /// <summary>
    ///     Refreshes the provided TModel instance with fresh data from the database.
    /// </summary>
    /// <param name="model"></param>
    /// <returns>
    ///     True if the TModel is backed by data in the database, else false.
    /// </returns>
    public bool Refresh(TModel model, CancellationToken cancellationToken = new());

    /// <summary>
    ///     Saves the current data of the TModel instance into the database.
    /// </summary>
    /// <param name="model">The model to save into the database.</param>
    /// <param name="saveOptions">How the model should be saved.</param>
    /// <exception cref="ModelExistsException">
    ///     Indicates that the TModel already exists in the DB when using Create mode.
    /// </exception>
    /// <exception cref="ModelDoesNotExistException">
    ///     Indicates that the TModel does not exist in the DB when using Replace mode.
    /// </exception>
    public void Save(TModel model, SaveOptions saveOptions = SaveOptions.CreateOrReplace, CancellationToken cancellationToken = new());
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