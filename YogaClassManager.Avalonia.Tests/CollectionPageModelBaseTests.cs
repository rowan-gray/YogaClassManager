using System.Reactive.Linq;
using ReactiveUI;
using YogaClassManager.Avalonia.ViewModels.Base;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Covers CollectionPageModelBase's own contract - selection restoration across a refresh, the
///     busy-depth wiring, and error surfacing. The error tests exist because subscribing to
///     RefreshCommand.ThrownExceptions stops ReactiveUI routing those exceptions to
///     RxApp.DefaultExceptionHandler: the base has to surface them itself or a failed load is silent
///     (see UI_REVIEW.md finding U2, which is what these tests lock down).
/// </summary>
public class CollectionPageModelBaseTests
{
    private class TestCollectionViewModel(FakeToastService toasts) : CollectionPageModelBase<string>(toasts)
    {
        public List<string> NextItems { get; set; } = [];
        public Exception? ThrowOnLoad { get; set; }
        public int LoadCount { get; private set; }

        protected override Task<IReadOnlyList<string>> LoadItemsAsync()
        {
            LoadCount++;

            if (ThrowOnLoad is not null)
                throw ThrowOnLoad;

            return Task.FromResult<IReadOnlyList<string>>(NextItems.ToList());
        }
    }

    private static (TestCollectionViewModel ViewModel, FakeToastService Toasts) Create()
    {
        var toasts = new FakeToastService();
        return (new TestCollectionViewModel(toasts), toasts);
    }

    [Fact]
    public async Task Refresh_ReplacesItems_AndSelectsFirstWhenNothingWasSelected()
    {
        var (viewModel, _) = Create();
        viewModel.NextItems = ["a", "b", "c"];

        await viewModel.RefreshCommand.Execute();

        Assert.Equal(["a", "b", "c"], viewModel.Items);
        Assert.Equal("a", viewModel.Selection);
    }

    [Fact]
    public async Task Refresh_KeepsPreviousSelection_WhenItSurvivesTheReload()
    {
        var (viewModel, _) = Create();
        viewModel.NextItems = ["a", "b", "c"];
        await viewModel.RefreshCommand.Execute();

        viewModel.Selection = "b";
        await viewModel.RefreshCommand.Execute();

        Assert.Equal("b", viewModel.Selection);
    }

    [Fact]
    public async Task Refresh_FallsBackToFirstItem_WhenPreviousSelectionIsGone()
    {
        var (viewModel, _) = Create();
        viewModel.NextItems = ["a", "b", "c"];
        await viewModel.RefreshCommand.Execute();
        viewModel.Selection = "c";

        viewModel.NextItems = ["a", "b"];
        await viewModel.RefreshCommand.Execute();

        Assert.Equal("a", viewModel.Selection);
    }

    [Fact]
    public async Task Refresh_LeavesSelectionNull_WhenTheListIsEmpty()
    {
        var (viewModel, _) = Create();

        await viewModel.RefreshCommand.Execute();

        Assert.Empty(viewModel.Items);
        Assert.Null(viewModel.Selection);
    }

    [Fact]
    public async Task Refresh_ClearsBusy_OnceItCompletes()
    {
        var (viewModel, _) = Create();
        viewModel.NextItems = ["a"];

        await viewModel.RefreshCommand.Execute();

        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void FailedLoad_SetsLastError()
    {
        var (viewModel, _) = Create();
        viewModel.ThrowOnLoad = new InvalidOperationException("database unavailable");

        viewModel.RefreshCommand.Execute().Subscribe(_ => { }, _ => { });

        Assert.Equal("database unavailable", viewModel.LastError);
    }

    [Fact]
    public void FailedLoad_RaisesAnErrorToast()
    {
        var (viewModel, toasts) = Create();
        viewModel.ThrowOnLoad = new InvalidOperationException("database unavailable");

        viewModel.RefreshCommand.Execute().Subscribe(_ => { }, _ => { });

        var error = Assert.Single(toasts.Errors);
        Assert.Contains("database unavailable", error);
    }

    [Fact]
    public void FailedLoad_ClearsBusy_SoThePageDoesNotStickOnASpinner()
    {
        var (viewModel, _) = Create();
        viewModel.ThrowOnLoad = new InvalidOperationException("boom");

        viewModel.RefreshCommand.Execute().Subscribe(_ => { }, _ => { });

        Assert.False(viewModel.IsBusy);
    }
}
