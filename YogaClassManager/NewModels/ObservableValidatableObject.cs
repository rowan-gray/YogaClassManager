using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace YogaClassManager.Models;

public abstract partial class ObservableValidatableObject : ObservableValidator
{
    [ObservableProperty] private string? error;

    [RelayCommand]
    public void Validate()
    {
        ValidateAllProperties();

        if (HasErrors)
        {
            Error = string.Join(Environment.NewLine, GetErrors().Select(e => e.ErrorMessage));
        }

        Error = null;
    }

    public bool IsValid()
    {
        Validate();

        return Error == null;
    }
}