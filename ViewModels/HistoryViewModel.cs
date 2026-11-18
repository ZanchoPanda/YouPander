
using System.Collections.ObjectModel;
using System.ComponentModel;
using YouPander.Models;
using YouPander.Services;
using static YouPander.Resources.Enums.EnumsYouPander;

namespace YouPander.ViewModels
{
    public class HistoryViewModel : BaseViewModel, IDisposable
    {
        #region Services

        private readonly HistoryService _history;

        #endregion

        #region Fields

        private List<DownloadRecord> _allRecords = new();

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplyFilters();
            }
        }

        public int SelectedFilterIndex
        {
            get => (int)SelectedFilter;
            set
            {
                if (Enum.IsDefined(typeof(HistoryFilter), value))
                    SelectedFilter = (HistoryFilter)value;
            }
        }

        private HistoryFilter _selectedFilter = HistoryFilter.All;
        public HistoryFilter SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (SetProperty(ref _selectedFilter, value))
                    ApplyFilters();
            }
        }

        private IReadOnlyList<string> _filterOptions = Array.Empty<string>();
        public IReadOnlyList<string> FilterOptions
        {
            get => _filterOptions;
            private set => SetProperty(ref _filterOptions, value);
        }

        public ObservableCollection<DownloadRecord> Records { get; } = new();

        #endregion

        #region Commands

        public Command LoadCommand { get; }
        public Command<DownloadRecord> DeleteCommand { get; }
        public Command ClearAllCommand { get; }
        public Command<DownloadRecord> ReDownloadCommand { get; }
        public Command<DownloadRecord> OpenUrlCommand { get; }

        #endregion

        #region Constructor

        public HistoryViewModel(HistoryService history)
        {
            _history = history;

            LoadCommand = new Command(async () => await LoadAsync());

            DeleteCommand = new Command<DownloadRecord>(async record => await DeleteAsync(record));

            ClearAllCommand = new Command(async () => await ClearAllAsync());

            ReDownloadCommand = new Command<DownloadRecord>(async record => await ReDownloadAsync(record));

            OpenUrlCommand = new Command<DownloadRecord>(async record => await OpenUrlAsync(record));

            UpdateFilterOptions();

            LocalizationResourceManager.Instance.PropertyChanged += OnLanguageChanged;
        }

        #endregion

        #region Localization

        private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(UpdateFilterOptions);
        }

        private void UpdateFilterOptions()
        {
            FilterOptions = new[]
            {
                LocalizationResourceManager.Instance["All"],
                LocalizationResourceManager.Instance["Today"],
                LocalizationResourceManager.Instance["ThisWeek"],
                LocalizationResourceManager.Instance["ThisMonth"]
            };
        }

        #endregion

        #region Loading

        public async Task LoadAsync()
        {
            try
            {
                var records = await _history.GetAllAsync();

                _allRecords = records ?? new List<DownloadRecord>();

                ApplyFilters();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading history: {ex.Message}");
            }
        }

        #endregion

        #region Filtering

        private void ApplyFilters()
        {
            var now = DateTime.Now;

            // Calculate natural date ranges
            var startOfWeek = StartOfWeek(now, DayOfWeek.Monday);
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            var filtered = _allRecords.Where(record =>
            {
                var date = record.DownloadedAt.ToLocalTime();

                // Date filter
                bool matchesDate = SelectedFilter switch
                {
                    HistoryFilter.Today => date.Date == now.Date,

                    HistoryFilter.ThisWeek => date >= startOfWeek && date < startOfWeek.AddDays(7),

                    HistoryFilter.ThisMonth => date >= startOfMonth && date < startOfMonth.AddMonths(1),

                    _ => true
                };

                // Text filter
                bool matchesSearch =
                    string.IsNullOrWhiteSpace(SearchText) || 
                        (record.Title?.Contains(SearchText.Trim(),
                            StringComparison.OrdinalIgnoreCase) 
                            ?? false) ||
                        (record.Channel?.Contains(SearchText.Trim(),StringComparison.OrdinalIgnoreCase) 
                            ?? false);

                return matchesDate && matchesSearch;

            })
            .OrderByDescending(record => record.DownloadedAt)
            .ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Records.Clear();

                foreach (var record in filtered)
                {
                    Records.Add(record);
                }
            });
        }

        private static DateTime StartOfWeek(
            DateTime date,
            DayOfWeek startOfWeek)
        {
            int difference = (7 + (date.DayOfWeek - startOfWeek)) % 7;

            return date.Date.AddDays(-difference);
        }

        #endregion

        #region Actions

        private async Task DeleteAsync(DownloadRecord? record)
        {
            if (record is null)
            {
                return;
            }

            try
            {
                await _history.DeleteAsync(record);

                _allRecords.Remove(record);
                Records.Remove(record);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting history record: {ex.Message}");
            }
        }

        private async Task ClearAllAsync()
        {
            try
            {
                await _history.ClearAllAsync();

                _allRecords.Clear();
                Records.Clear();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing history: {ex.Message}");
            }
        }

        private async Task ReDownloadAsync(DownloadRecord? record)
        {
            if (record is null || string.IsNullOrWhiteSpace(record.Url))
            {
                return;
            }

            await Shell.Current.GoToAsync($"///MainPage?url={Uri.EscapeDataString(record.Url)}");
        }

        private async Task OpenUrlAsync(DownloadRecord? record)
        {
            if (record is null || string.IsNullOrWhiteSpace(record.Url))
            {
                return;
            }

            await Shell.Current.GoToAsync($"BrowserPage?url={Uri.EscapeDataString(record.Url)}");
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            LocalizationResourceManager.Instance.PropertyChanged -=OnLanguageChanged;
        }

        #endregion
    }
}
