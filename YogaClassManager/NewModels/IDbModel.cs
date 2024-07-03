namespace YogaClassManager.NewModels;

public interface IDbModel<TModel, TFilter>
{
    enum SaveOptions
    {
        Create,
        CreateOrReplace,
        Replace
    }

    public static abstract Task<TModel> LoadSingle(TFilter filter);

    /// <summary>
    ///     Loads multiple instances of the TModel from the database.
    /// </summary>
    /// <param name="filter">The filter used to select which instances to load.</param>
    /// <param name="count">The number of instances to load.</param>
    /// <param name="skip">The number of instances to skip over.</param>
    /// <returns></returns>
    public static abstract Task<IEnumerable<TModel>> LoadMultiple(TFilter filter,
        uint count = uint.MaxValue, uint skip = 0);

    /// <summary>
    ///     Refreshes the provided TModel instance with fresh data from the database.
    /// </summary>
    /// <param name="model"></param>
    /// <returns>
    ///     True if the TModel is backed by data in the database, else false.
    /// </returns>
    public static abstract bool Refresh(TModel model);

    /// <summary>
    ///     Saves the current data of the TModel instance into the database.
    /// </summary>
    /// <exception cref="ModelExistsException">
    ///     Indicates that the TModel already exists in the DB when using Create mode.
    /// </exception>
    /// <exception cref="ModelDoesNotExistException">
    ///     Indicates that the TModel does not exist in the DB when using Replace mode.
    /// </exception>
    public static abstract void Save(TModel model, SaveOptions saveOptions = SaveOptions.CreateOrReplace);
}

public class ModelExistsException : Exception
{
}

public class ModelDoesNotExistException : Exception
{
}