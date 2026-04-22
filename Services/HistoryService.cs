using SQLite;
using System;
using System.Collections.Generic;
using System.Text;
using YouPander.Models;

namespace YouPander.Services
{
    public class HistoryService
    {

        private readonly SQLiteAsyncConnection _db;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public HistoryService()
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "history.db");
            _db = new SQLiteAsyncConnection(dbPath);
            _db.CreateTableAsync<DownloadRecord>().Wait();
        }

        public async Task AddAsync(DownloadRecord record)
        {
            await _lock.WaitAsync();
            try
            {
                await _db.InsertAsync(record);
            }
            finally
            {
                _lock.Release();
            }
        } 

        public Task<List<DownloadRecord>> GetAllAsync() => _db.Table<DownloadRecord>()
                                                                .OrderByDescending(r => r.DownloadedAt)
                                                                .ToListAsync();

        public Task DeleteAsync(DownloadRecord record) => _db.DeleteAsync(record);

        public Task ClearAllAsync() => _db.DeleteAllAsync<DownloadRecord>();

    }
}
