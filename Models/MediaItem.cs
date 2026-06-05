using System;
using System.Collections.Generic;
using System.Text;

namespace YouPander.Models
{
    public class MediaItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string Title => Path.GetFileNameWithoutExtension(FilePath);
        public string FileName => Path.GetFileName(FilePath);
        public bool IsVideo { get; set; }

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".m4v" };

        private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".aac", ".flac", ".ogg", ".wav", ".m4a", ".opus" };

        public static MediaItem? TryCreate(string path)
        {
            var ext = Path.GetExtension(path);

            if (VideoExtensions.Contains(ext))
            {
                return new MediaItem { FilePath = path, IsVideo = true };
            }

            if (AudioExtensions.Contains(ext))
            {
                return new MediaItem { FilePath = path, IsVideo = false };
            }

            return null;
        }
    }
}
