using SQLite;
using System;
using System.Collections.Generic;
using System.Text;

namespace YouPander.Models
{
    [Table("DownloadHistory")]
    public class DownloadRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;   // "Audio" / "Video"
        public string Quality { get; set; } = string.Empty;
        public string DownloadPath { get; set; } = string.Empty;
        public DateTime DownloadedAt { get; set; } = DateTime.Now;
        public bool Success { get; set; }
    }
}
