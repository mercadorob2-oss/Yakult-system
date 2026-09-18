using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Wpf.Search.ViewModels;

namespace Yakult.Inventory.App.Wpf.Search.Views
{
    public partial class SearchView : UserControl
    {
        private SearchViewModel _vm;

        /// <summary>Raised when the user clicks "Go to [Destination]" on a selected card.</summary>
        public event Action<ItemDto, string, string> NavigateRequested;

        // Cancels the in-flight debounce/DB fetch for the AutoCompleteBox suggestions whenever the
        // user types another character — only ever one suggestion request is "live" at a time.
        private CancellationTokenSource _suggestCts;

        // Set while an arrow key is programmatically moving AutoCompleteBox.SelectedItem, so that
        // SelectionChanged (which normally means "the user committed to this suggestion") doesn't
        // fire a full search on every highlight step — only Enter/click should do that.
        private bool _suppressSelectionChanged;

        public SearchView()
        {
            InitializeComponent();

            _vm = new SearchViewModel();
            _vm.NavigateAction = (item, dest, title) => NavigateRequested?.Invoke(item, dest, title);
            DataContext = _vm;

            Loaded   += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadLogo();
            CenterAutoBox.Focus();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _suggestCts?.Cancel();
            // Cancel any in-flight search/suggest operations
            _vm.ResetView();
        }

        private void LoadLogo()
        {
            var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "yakult_Name.png");
            if (File.Exists(logoPath))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource   = new Uri(logoPath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    LogoImage.Source = bmp;
                }
                catch { }
            }
        }

        // ── AutoCompleteBox suggestions — debounced, cancellable, off-thread ───────────────────
        //
        // FilterMode="None" on the XAML control disables the box's own built-in text filtering so
        // every query goes through here instead. The pattern per keystroke is:
        //   1. e.Cancel = true          — tell the control we'll supply results asynchronously
        //   2. cancel the previous in-flight suggestion request (only the latest keystroke matters)
        //   3. Task.Delay(300ms)        — debounce; if the user types again before this elapses,
        //                                 the delay's own cancellation token throws and we bail out
        //   4. await the repository     — SqlCommand's *Async methods already run off the UI thread
        //                                 via IO completion ports, so the UI never blocks/stutters
        //   5. box.PopulateComplete()   — tells the control the async populate is done and it may
        //                                 open its dropdown
        private async void SearchAutoBox_Populating(object sender, PopulatingEventArgs e)
        {
            e.Cancel = true;
            var box = (AutoCompleteBox)sender;
            string text = e.Parameter?.Trim();

            _suggestCts?.Cancel();

            if (string.IsNullOrEmpty(text) || text.Length < 2)
            {
                _vm.Suggestions.Clear();
                box.PopulateComplete();
                return;
            }

            var cts = new CancellationTokenSource();
            _suggestCts = cts;
            var token = cts.Token;

            try
            {
                await Task.Delay(300, token);
            }
            catch (TaskCanceledException)
            {
                return; // superseded by a newer keystroke — do not touch the dropdown
            }

            List<SearchSuggestionDto> results;
            try
            {
                var repo = new SearchRepository();
                results = await repo.GetRichSearchSuggestionsAsync(text, 15, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // DB connectivity/timeout errors degrade to "no suggestions", never a crash.
                results = new List<SearchSuggestionDto>();
            }

            if (token.IsCancellationRequested) return;

            _vm.Suggestions.Clear();
            foreach (var r in results) _vm.Suggestions.Add(r);
            box.PopulateComplete();
        }

        private void SearchAutoBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged) return; // arrow-key highlight step, not a commit

            // Read the newly-selected row straight off the event args rather than re-querying
            // box.SelectedItem: e.AddedItems is the authoritative "what was just picked" for this
            // specific event, so it can't pick up a stale/different SelectedItem left over from a
            // moment earlier (e.g. from a hover or an in-flight arrow-key highlight step).
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is SearchSuggestionDto suggestion)
                _vm.SelectSuggestion(suggestion);
        }

        private void SearchAutoBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var box = (AutoCompleteBox)sender;

            // Up/Down: move the highlighted suggestion without running a search yet (mirrors how a
            // native combo/autocomplete box behaves — arrowing through options previews, it doesn't commit).
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                if (_vm.Suggestions.Count == 0) return;
                e.Handled = true;

                // Drive the inner ListBox ("Selector" template part) directly rather than only setting
                // AutoCompleteBox.SelectedItem: going through the control's own selection adapter alone
                // changed the reported SelectedItem but did not reliably realize/select the corresponding
                // ListBoxItem container, so the IsSelected highlight in AutoBoxItemStyle never lit up.
                // Setting SelectedIndex on the real ListBox — plus ScrollIntoView — guarantees both the
                // highlight and that a highlighted-but-offscreen row scrolls into the visible dropdown area.
                var listBox = box.Template?.FindName("Selector", box) as ListBox;
                if (listBox == null) return;

                int count = _vm.Suggestions.Count;
                int index = listBox.SelectedIndex;
                index = e.Key == Key.Down
                    ? (index + 1) % count
                    : (index <= 0 ? count - 1 : index - 1);

                _suppressSelectionChanged = true;
                listBox.SelectedIndex = index;
                listBox.ScrollIntoView(listBox.SelectedItem);
                box.SelectedItem = listBox.SelectedItem; // keep the control's own state in sync for Enter/click
                _suppressSelectionChanged = false;
                return;
            }

            if (e.Key != Key.Enter) return;
            e.Handled = true;

            // If the user arrowed onto a highlighted suggestion, Enter commits that suggestion (same
            // as clicking it). Otherwise Enter runs a full-text search on whatever was typed — this
            // must fire regardless of whether the dropdown happens to be open, since typing something
            // with no matching suggestions (or just not having waited for the debounce) should still search.
            if (box.SelectedItem is SearchSuggestionDto suggestion)
                _vm.SelectSuggestion(suggestion);
            else if (!string.IsNullOrWhiteSpace(box.Text))
                _vm.ExecuteSearch(box.Text);
        }

        private void JumpToPageBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _vm.GoToPageCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
