using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;
using YogaClassManager.Avalonia.ViewModels.Base;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Covers the two helpers CollectionPageModelBase gained for UI_REVIEW.md A1/A2/A6 - the
///     child-list reload (<c>ReloadOn</c>) and the filter-change reload (<c>RefreshWhenChanged</c>).
///
///     Both were previously hand-written in five ViewModels as
///     <c>WhenAnyValue(...).SelectMany(async _ =&gt; await LoadX()).Subscribe()</c>, which merges
///     concurrent loads and dies permanently on the first exception. Those are the two behaviours these
///     tests pin down; they use a controllable load rather than a repository because the in-memory
///     repositories complete synchronously, which is exactly why the bug was invisible in the app.
/// </summary>
public class CollectionPageReloadTests
{
    private class TestPageViewModel : CollectionPageModelBase<string>
    {
        private readonly Subject<int> trigger = new();

        public TestPageViewModel(FakeToastService toasts) : base(toasts)
        {
            ReloadOn(trigger, LoadChildAsync, "the child list");
            RefreshWhenChanged(this.WhenAnyValue(x => x.Filter));
        }

        /// <summary>Completed by the test, so two loads can deliberately be in flight at once.</summary>
        public List<TaskCompletionSource> Pending { get; } = [];

        public List<string> Applied { get; } = [];
        public int RefreshCount { get; private set; }
        public bool FailNextChildLoad { get; set; }

        private string filter = "";

        public string Filter
        {
            get => filter;
            set => this.RaiseAndSetIfChanged(ref filter, value);
        }

        public void Trigger(int value) => trigger.OnNext(value);

        protected override Task<IReadOnlyList<string>> LoadItemsAsync()
        {
            RefreshCount++;
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        private async Task LoadChildAsync(CancellationToken cancellationToken)
        {
            var gate = new TaskCompletionSource();
            Pending.Add(gate);
            var index = Pending.Count;

            await gate.Task;

            if (FailNextChildLoad)
            {
                FailNextChildLoad = false;
                throw new InvalidOperationException("child load failed");
            }

            // Mirrors what the real loads do: the write, not the return value, is the output, so a
            // superseded load has to check the token before touching shared state.
            cancellationToken.ThrowIfCancellationRequested();
            Applied.Add($"load{index}");
        }
    }

    private static (TestPageViewModel ViewModel, FakeToastService Toasts) Create()
    {
        var toasts = new FakeToastService();
        return (new TestPageViewModel(toasts), toasts);
    }

    /// <summary>The load's own writes resume inline on the thread that completes the gate, but Rx
    /// bridges a *faulted* Task to OnError through a Task continuation, which can land on the thread
    /// pool - so error assertions wait rather than assuming same-thread delivery.</summary>
    private static void WaitFor(Func<bool> condition, string expectation)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            Thread.Sleep(5);

        Assert.True(condition(), $"timed out waiting for {expectation}");
    }

    [Fact]
    public void ReloadOn_DropsAnInFlightLoad_WhenTheTriggerFiresAgain()
    {
        var (viewModel, _) = Create();

        viewModel.Trigger(1);
        viewModel.Trigger(2);
        Assert.Equal(2, viewModel.Pending.Count);

        // Finish the superseded load last, the interleaving that made a merged (SelectMany) chain show
        // one record's linked data under another's header.
        viewModel.Pending[1].SetResult();
        viewModel.Pending[0].SetResult();

        Assert.Equal(["load2"], viewModel.Applied);
    }

    [Fact]
    public void ReloadOn_KeepsWorking_AfterALoadFails()
    {
        var (viewModel, toasts) = Create();

        viewModel.FailNextChildLoad = true;
        viewModel.Trigger(1);
        viewModel.Pending[0].SetResult();

        WaitFor(() => toasts.Errors.Count == 1, "the failed load to be reported");
        Assert.Contains("child load failed", toasts.Errors[0]);
        Assert.Equal("child load failed", viewModel.LastError);

        // Without an onError handler the subscription would be dead by now, and this second load would
        // never run for the rest of the ViewModel's life.
        viewModel.Trigger(2);
        viewModel.Pending[1].SetResult();

        Assert.Equal(["load2"], viewModel.Applied);
    }

    [Fact]
    public void RefreshWhenChanged_DoesNotFire_OnTheInitialReplayedValue()
    {
        var (viewModel, _) = Create();

        // WhenAnyValue replays the current value on subscription; the Skip(1) inside the helper is what
        // stops that phantom change reloading the list right after construction.
        Assert.Equal(0, viewModel.RefreshCount);
    }

    [Fact]
    public void RefreshWhenChanged_ReloadsOnARealChange()
    {
        var (viewModel, _) = Create();

        viewModel.Filter = "yoga";

        Assert.Equal(1, viewModel.RefreshCount);
    }
}
