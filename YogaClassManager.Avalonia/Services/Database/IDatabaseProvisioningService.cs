namespace YogaClassManager.Avalonia.Services.Database;

/// <summary>Given a path the user/settings intend to use, makes sure a working, up-to-date database
/// exists there - backing up first if a migration is about to run. Deliberately handles "open an
/// existing file" and "create a new file" identically: the only difference between those two intents
/// is which native picker produced the path, and by the time it reaches this service both are treated
/// the same way.</summary>
public interface IDatabaseProvisioningService
{
    Task<ProvisionResult> ProvisionAsync(string path, CancellationToken cancellationToken = default);
}
