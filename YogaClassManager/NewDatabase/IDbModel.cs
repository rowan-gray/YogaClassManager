namespace YogaClassManager.NewDatabase;

public interface IDbModel<TModel, TFilter>
{
    /// <summary>
    ///     Loads a single instance of the TModel from the database.
    /// </summary>
    /// <param name="dbService">The database service to use.</param>
    /// <param name="filter">The filter used to select which instance to load.</param>
    /// <returns>
    ///     The model matching the provided filter. 
    /// </returns>
    public static abstract Task<TModel?> LoadSingle(DatabaseService dbService, 
        TFilter? filter = default);

    /// <summary>
    ///     Loads multiple instances of the TModel from the database.
    /// </summary>
    /// <param name="dbService">The database service to use.</param>
    /// <param name="filter">The filter used to select which instances to load.</param>
    /// <param name="count">The number of instances to load.</param>
    /// <param name="skip">The number of instances to skip over.</param>
    /// <returns>
    ///     A list of models matching the provided filter.
    /// </returns>
    public static abstract Task<IEnumerable<TModel>> LoadMultiple(DatabaseService dbService, 
        TFilter? filter = default, uint count = uint.MaxValue, uint skip = 0);

    /// <summary>
    ///     Refreshes the provided TModel instance with fresh data from the database.
    /// </summary>
    /// <param name="dbService">The database service to use.</param>
    /// <param name="model"></param>
    /// <returns>
    ///     True if the TModel is backed by data in the database, else false.
    /// </returns>
    public static abstract bool Refresh(DatabaseService dbService, TModel model);

    /// <summary>
    ///     Saves the current data of the TModel instance into the database.
    /// </summary>
    /// <param name=""></param>
    /// <param name="dbService">The database service to use.</param>
    /// <param name="model">The model to save into the database.</param>
    /// <param name="saveOptions">How the model should be saved.</param>
    /// <exception cref="ModelExistsException">
    ///     Indicates that the TModel already exists in the DB when using Create mode.
    /// </exception>
    /// <exception cref="ModelDoesNotExistException">
    ///     Indicates that the TModel does not exist in the DB when using Replace mode.
    /// </exception>
    public static abstract void Save(DatabaseService dbService, TModel model, 
        SaveOptions saveOptions = SaveOptions.CreateOrReplace);
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