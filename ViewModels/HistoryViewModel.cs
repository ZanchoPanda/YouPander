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
            Records.Clear();
            var items = await _history.GetAllAsync();
            foreach (var item in items)
                Records.Add(item);
        }

        private async Task DeleteAsync(DownloadRecord record)
        {
            await _history.DeleteAsync(record);
            Records.Remove(record);
        }

        private async Task ClearAllAsync()
        {
            await _history.ClearAllAsync();
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
