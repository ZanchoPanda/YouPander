using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using YouPander.Models;
using YouPander.Services;

namespace YouPander.ViewModels
{
    public class HistoryViewModel : BaseViewModel
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

        private int _selectedFilterIndex = 0;
        public int SelectedFilterIndex
        {
            get => _selectedFilterIndex;
            set
            {
                if (SetProperty(ref _selectedFilterIndex, value))
                    ApplyFilters();
            }
        }

        public List<string> FilterOptions { get; } = ["Todo", "Hoy", "Esta semana", "Este mes"];

        public ObservableCollection<DownloadRecord> Records { get; } = new();

        public Command LoadCommand { get; }
        public Command<DownloadRecord> DeleteCommand { get; }
        public Command ClearAllCommand { get; }
        public Command<DownloadRecord> ReDownloadCommand { get; }

        #endregion

        public HistoryViewModel(HistoryService history)
        {
            _history = history;

            LoadCommand = new Command(async () => await LoadAsync());
            DeleteCommand = new Command<DownloadRecord>(async (r) => await DeleteAsync(r));
            ClearAllCommand = new Command(async () => await ClearAllAsync());
            ReDownloadCommand = new Command<DownloadRecord>(async (r) => await ReDownloadAsync(r));
        }

        #region Actions Commands


        public async Task LoadAsync()
        {
            #region Version 1
            //Records.Clear();
            //var items = await _history.GetAllAsync();
            //foreach (var item in items)
            //    Records.Add(item);
            #endregion

            #region V2
            _allRecords = await _history.GetAllAsync();
            ApplyFilters();
            #endregion
        }

        private void ApplyFilters()
        {
            var filtered = _allRecords.AsEnumerable();

            // Filtro por fecha
            filtered = SelectedFilterIndex switch
            {
                1 => filtered.Where(r => r.DownloadedAt.ToLocalTime().Date == DateTime.Today),
                2 => filtered.Where(r => r.DownloadedAt.ToLocalTime() >= DateTime.Today.AddDays(-7)),
                3 => filtered.Where(r => r.DownloadedAt.ToLocalTime() >= DateTime.Today.AddDays(-30)),
                _ => filtered
            };

            // Filtro por texto
            if (!string.IsNullOrWhiteSpace(SearchText))
                filtered = filtered.Where(r =>
                    r.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    r.Channel.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            Records.Clear();
            foreach (var item in filtered)
                Records.Add(item);
        }

        private async Task DeleteAsync(DownloadRecord record)
        {
            await _history.DeleteAsync(record);
            _allRecords.Remove(record);
            Records.Remove(record);
        }

        private async Task ClearAllAsync()
        {
            await _history.ClearAllAsync();
            _allRecords.Clear();
            Records.Clear();
        }

        private async Task ReDownloadAsync(DownloadRecord record)
        {
            // Navigate to MainPage with preloaded URL
            await Shell.Current.GoToAsync($"///MainPage?url={Uri.EscapeDataString(record.Url)}");
        }

        #endregion

    }
}
