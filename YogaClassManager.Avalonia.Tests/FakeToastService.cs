using Avalonia.Controls;
using YogaClassManager.Avalonia.Services;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Records toasts instead of showing them, so ViewModel tests can assert what the user would have
///     been told. Note AttachHost forces this hand-written double to reference Avalonia.Controls even
///     though no test needs a TopLevel - see UI_REVIEW.md finding A7 (the host-attachment lifecycle
///     concern belongs on a separate interface from the one ViewModels depend on).
/// </summary>
public class FakeToastService : IToastService
{
    public List<string> Infos { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Errors { get; } = [];

    public void AttachHost(TopLevel topLevel)
    {
    }

    public void ShowInfo(string message) => Infos.Add(message);
    public void ShowWarning(string message) => Warnings.Add(message);
    public void ShowError(string message) => Errors.Add(message);
}
