using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Core.Models.People;
using YogaClassManager.Core.Repositories;

namespace YogaClassManager.Avalonia.ViewModels.Shared;

public class StudentEditViewModel : DialogViewModelBase<Student>
{
    private readonly IIdentityRepository identityRepository;
    private readonly IDialogService dialogService;
    private int id;
    private string firstName;
    private string? lastName;
    private string? phoneNumber;
    private string? email;
    private string? validationError;

    public StudentEditViewModel(Student editingStudent, bool isNew, IIdentityRepository identityRepository,
        IDialogService dialogService)
    {
        IsNew = isNew;
        this.identityRepository = identityRepository;
        this.dialogService = dialogService;
        id = editingStudent.Id;
        IsActive = editingStudent.IsActive;
        firstName = editingStudent.FirstName;
        lastName = editingStudent.LastName;
        phoneNumber = editingStudent.PhoneNumber;
        email = editingStudent.Email;

        SaveCommand = ReactiveCommand.Create(Save);
        DefaultCommand = SaveCommand;
        UseExistingIdentityCommand = ReactiveCommand.CreateFromTask(UseExistingIdentityAsync,
            Observable.Return(isNew));
    }

    public bool IsNew { get; }
    public string Title => IsNew ? "Add student" : "Edit student";
    public bool IsActive { get; }

    /// <summary>
    ///     Starts at the editing Student's own id (-1 for a brand-new student). Set by
    ///     <see cref="UseExistingIdentityAsync" /> to an existing, non-Student Identity's id when the
    ///     instructor picks "use existing identity" - StudentsViewModel.AddStudentAsync checks whether
    ///     the Student this dialog returns has a positive Id to decide whether to promote that existing
    ///     Identity (via IStudentRepository.PromoteToStudentAsync) instead of creating a new row.
    /// </summary>
    public int Id
    {
        get => id;
        private set => this.RaiseAndSetIfChanged(ref id, value);
    }

    public bool IsConvertingExistingIdentity => Id > 0;

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
    public ReactiveCommand<Unit, Unit> UseExistingIdentityCommand { get; }

    private async Task UseExistingIdentityAsync()
    {
        var pickerViewModel = new IdentityPickerViewModel(identityRepository,
            "Select an identity to convert to a student", excludeStudents: true);
        var identity = await dialogService.ShowDialogAsync(pickerViewModel);
        if (identity is null)
            return;

        Id = identity.Id;
        FirstName = identity.FirstName;
        LastName = identity.LastName;
        PhoneNumber = identity.PhoneNumber;
        Email = identity.Email;
        ValidationError = null;
        this.RaisePropertyChanged(nameof(IsConvertingExistingIdentity));
    }

    private void Save()
    {
        var student = new Student(Id, FirstName, LastName ?? "", PhoneNumber, Email, IsActive);

        if (!student.Validate())
        {
            ValidationError = "First and last name are required, and at least one valid phone number or " +
                               "email is required.";
            return;
        }

        Close(student);
    }
}
