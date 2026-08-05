using Microsoft.Reactive.Testing;
using ReactiveUI;
using YogaClassManager.Avalonia.ViewModels.Base;

namespace YogaClassManager.Avalonia.Tests;

/// <summary>
///     Verifies the debounced-search behaviour SearchableCollectionPageModelBase adds over the MAUI
///     app's original SearchableCollectionPageModel&lt;T&gt; (which had no debounce at all). Runs
///     against a TestScheduler substituted in for RxApp.MainThreadScheduler so the 300ms throttle can
///     be exercised deterministically without real delays.
/// </summary>
public class SearchableCollectionPageModelBaseTests
{
    private class TestSearchViewModel(FakeToastService toasts) : SearchableCollectionPageModelBase<string>(toasts)
    {
        public TestSearchViewModel() : this(new FakeToastService())
        {
        }

        public int LoadCount { get; private set; }
        public string? LastQuerySeen { get; private set; }

        protected override Task<IReadOnlyList<string>> LoadItemsAsync()
        {
            LoadCount++;
            LastQuerySeen = SearchQuery;
            return Task.FromResult<IReadOnlyList<string>>([SearchQuery ?? ""]);
        }
    }

    private static TResult WithTestScheduler<TResult>(Func<TestScheduler, TResult> body)
    {
        var testScheduler = new TestScheduler();
        var originalScheduler = RxApp.MainThreadScheduler;
        RxApp.MainThreadScheduler = testScheduler;
        try
        {
            return body(testScheduler);
        }
        finally
        {
            RxApp.MainThreadScheduler = originalScheduler;
        }
    }

    [Fact]
    public void RapidChanges_WithinThrottleWindow_ProduceOnlyOneLoad()
    {
        WithTestScheduler(scheduler =>
        {
            var viewModel = new TestSearchViewModel();

            viewModel.SearchQuery = "a";
            scheduler.AdvanceByMs(100);
            viewModel.SearchQuery = "ab";
            scheduler.AdvanceByMs(100);
            viewModel.SearchQuery = "abc";
            scheduler.AdvanceByMs(100);

            Assert.Equal(0, viewModel.LoadCount);

            scheduler.AdvanceByMs(250);

            Assert.Equal(1, viewModel.LoadCount);
            Assert.Equal("abc", viewModel.LastQuerySeen);
            return true;
        });
    }

    [Fact]
    public void ChangesFartherApartThanThrottleWindow_EachProduceALoad()
    {
        WithTestScheduler(scheduler =>
        {
            var viewModel = new TestSearchViewModel();

            viewModel.SearchQuery = "first";
            scheduler.AdvanceByMs(350);
            Assert.Equal(1, viewModel.LoadCount);

            viewModel.SearchQuery = "second";
            scheduler.AdvanceByMs(350);
            Assert.Equal(2, viewModel.LoadCount);

            return true;
        });
    }

    [Fact]
    public void RepeatingTheSameQuery_AfterThrottling_DoesNotLoadAgain()
    {
        WithTestScheduler(scheduler =>
        {
            var viewModel = new TestSearchViewModel();

            viewModel.SearchQuery = "same";
            scheduler.AdvanceByMs(350);
            Assert.Equal(1, viewModel.LoadCount);

            viewModel.SearchQuery = "same";
            scheduler.AdvanceByMs(350);

            Assert.Equal(1, viewModel.LoadCount);
            return true;
        });
    }

    /// <summary>
    ///     WhenAnyValue emits SearchQuery's initial null immediately on subscription, not just on
    ///     later changes. Without skipping that first emission, it rides the same throttle into a
    ///     redundant RefreshCommand ~300ms after every page load - rebuilding an already-populated,
    ///     already-correctly-selected list for no reason, which is what caused the list's selection
    ///     to visibly flash on navigation (see People/Students/Terms pages).
    /// </summary>
    [Fact]
    public void Construction_WithNoSearchQueryChange_NeverLoads()
    {
        WithTestScheduler(scheduler =>
        {
            var viewModel = new TestSearchViewModel();

            // Advance well past the 300ms throttle window with SearchQuery never touched.
            scheduler.AdvanceByMs(1000);

            Assert.Equal(0, viewModel.LoadCount);
            return true;
        });
    }

    [Fact]
    public void RealChange_AfterConstruction_StillLoadsExactlyOnce()
    {
        WithTestScheduler(scheduler =>
        {
            var viewModel = new TestSearchViewModel();

            // Let the (skipped) initial emission's would-be throttle window pass with no change.
            scheduler.AdvanceByMs(1000);
            Assert.Equal(0, viewModel.LoadCount);

            viewModel.SearchQuery = "query";
            scheduler.AdvanceByMs(350);

            Assert.Equal(1, viewModel.LoadCount);
            Assert.Equal("query", viewModel.LastQuerySeen);
            return true;
        });
    }
}
