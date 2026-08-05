using System.ComponentModel;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Answers dialogs with canned results instead of rendering them, and records which ViewModels were
///     shown. Lets a test drive "the user confirmed" vs "the user cancelled" for the confirmation
///     prompts guarding destructive actions.
/// </summary>
public class ScriptedDialogService : IDialogService
{
    private readonly Dictionary<Type, object?> resultsByType = new();

#pragma warning disable CS0067 // Required by IDialogService's INotifyPropertyChanged; no test observes it.
    public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

    public List<object> Shown { get; } = [];

    public object? ActiveDialog => null;
    public bool IsDialogOpen => false;

    /// <summary>Answer any dialog whose ViewModel is <typeparamref name="TViewModel" /> with
    /// <paramref name="result" />. Anything not scripted comes back as default (i.e. cancelled).</summary>
    public ScriptedDialogService Answer<TViewModel>(object? result)
    {
        resultsByType[typeof(TViewModel)] = result;
        return this;
    }

    public Task<TResult?> ShowDialogAsync<TResult>(DialogViewModelBase<TResult> viewModel)
    {
        Shown.Add(viewModel);

        if (resultsByType.TryGetValue(viewModel.GetType(), out var result))
            return Task.FromResult((TResult?)result);

        return Task.FromResult(default(TResult));
    }
}
