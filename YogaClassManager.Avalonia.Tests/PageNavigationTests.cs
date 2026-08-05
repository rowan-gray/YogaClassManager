using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Avalonia.Services;
using YogaClassManager.Avalonia.ViewModels.Identities;
using YogaClassManager.Core.Dummy;
using YogaClassManager.Core.Filters;
using YogaClassManager.Core.Models.People;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     UI_REVIEW.md A5, from the outside: a page ViewModel now depends on IPageNavigator rather than on
///     an unchecked <c>(AppScreen)hostScreen</c> downcast and a <c>Locator.Current</c> lookup of the
///     other page's ViewModel type.
///
///     The load-bearing detail in these tests is the setup, not the assertions - every one of them
///     constructs a ViewModel with a plain fake IScreen and no Splat container at all. Before the
///     refactor that threw InvalidCastException in the constructor.
/// </summary>
public class PageNavigationTests
{
    private class FakeScreen : IScreen
    {
        public RoutingState Router { get; } = new();
    }

    private static (IdentitiesViewModel ViewModel, FakeNavigator Navigator, InMemoryDataStore Store)
        CreateIdentities()
    {
        var store = new InMemoryDataStore();
        var identityRepository = new InMemoryIdentityRepository(store);
        var passRepository = new InMemoryPassRepository(store);
        var emergencyContactRepository = new InMemoryEmergencyContactRepository(store);
        var studentRepository = new InMemoryStudentRepository(store, identityRepository, passRepository,
            emergencyContactRepository);

        var navigator = new FakeNavigator();

        var viewModel = new IdentitiesViewModel(new FakeScreen(), identityRepository, studentRepository,
            emergencyContactRepository, new DialogService(), new FakeToastService(), navigator);

        return (viewModel, navigator, store);
    }

    [Fact]
    public void APageViewModel_ConstructsAgainstAPlainIScreen()
    {
        var (viewModel, _, _) = CreateIdentities();

        Assert.Equal("identities", viewModel.UrlPathSegment);
        Assert.NotNull(viewModel.HostScreen);
    }

    [Fact]
    public async Task ViewingALinkedStudent_GoesThroughTheNavigator()
    {
        var (viewModel, navigator, store) = CreateIdentities();
        var studentRepository = new InMemoryStudentRepository(store, new InMemoryIdentityRepository(store),
            new InMemoryPassRepository(store), new InMemoryEmergencyContactRepository(store));

        var studentId = await studentRepository.AddAsync(
            new Student(0, "Ada", "Lovelace", "0400000000", null, true));
        var student = await studentRepository.Query(new StudentFilter { Id = (uint)studentId }).LoadSingle();

        await viewModel.NavigateToStudentCommand.Execute(student!);

        var navigation = Assert.Single(navigator.Navigations);
        Assert.Equal(("student", studentId), navigation);
    }

    [Fact]
    public async Task APendingSelection_IsAppliedOnce_AndThenCleared()
    {
        var store = new InMemoryDataStore();
        var identityRepository = new InMemoryIdentityRepository(store);
        var wantedId = await identityRepository.AddAsync(new Identity(0, "Grace", "Hopper", "0400000001", null,
            true));
        await identityRepository.AddAsync(new Identity(0, "Ada", "Lovelace", "0400000002", null, true));

        var passRepository = new InMemoryPassRepository(store);
        var emergencyContactRepository = new InMemoryEmergencyContactRepository(store);
        var navigator = new FakeNavigator { PendingIdentitySelection = wantedId };

        var viewModel = new IdentitiesViewModel(new FakeScreen(), identityRepository,
            new InMemoryStudentRepository(store, identityRepository, passRepository, emergencyContactRepository),
            emergencyContactRepository, new DialogService(), new FakeToastService(), navigator);

        Assert.Equal(wantedId, viewModel.Selection?.Id);

        // Taking it clears it, so a later refresh doesn't snap the selection back to a row the user has
        // since navigated away from.
        Assert.Null(navigator.PendingIdentitySelection);
    }
}
