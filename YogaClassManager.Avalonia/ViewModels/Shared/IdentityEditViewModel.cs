using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

/// <summary>
///     Add/Edit form for an Identity - constructed with a fresh Identity() for "add" or an
///     Identity.Copy(...) for "edit" (the copy means Cancel discards changes without mutating the
///     shared store instance).
/// </summary>
public class IdentityEditViewModel : DialogViewModelBase<Identity>
{
    private string firstName;
    private string? lastName;
    private string? phoneNumber;
    private string? email;
    private string? validationError;

    public IdentityEditViewModel(Identity editingIdentity, bool isNew)
    {
        IsNew = isNew;
        firstName = editingIdentity.FirstName;
        lastName = editingIdentity.LastName;
        phoneNumber = editingIdentity.PhoneNumber;
        email = editingIdentity.Email;
        Id = editingIdentity.Id;
        IsActive = editingIdentity.IsActive;

        SaveCommand = ReactiveCommand.Create(Save);
        DefaultCommand = SaveCommand;
    }

    public bool IsNew { get; }
    public string Title => IsNew ? "Add identity" : "Edit identity";
    public int Id { get; }
    public bool IsActive { get; }

    public string FirstName
    {
        get => firstName;
        set => this.RaiseAndSetIfChanged(ref firstName, value);
    }

    public string? LastName
    {
        get => lastName;
        set => this.RaiseAndSetIfChanged(ref lastName, value);
    }

    public string? PhoneNumber
    {
        get => phoneNumber;
        set => this.RaiseAndSetIfChanged(ref phoneNumber, value);
    }

    public string? Email
    {
        get => email;
        set => this.RaiseAndSetIfChanged(ref email, value);
    }

    public string? ValidationError
    {
        get => validationError;
        private set => this.RaiseAndSetIfChanged(ref validationError, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    private void Save()
    {
        var identity = new Identity(Id, FirstName, LastName, PhoneNumber, Email, IsActive);

        if (!identity.Validate())
        {
            ValidationError = "First name is required, and at least one valid phone number or email is required.";
            return;
        }

        Close(identity);
    }
}
