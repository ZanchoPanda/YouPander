using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;

namespace YouPander.Models
{
    public partial class MediaItem : ObservableObject
    {
        public string FilePath { get; set; } = string.Empty;

        public string Title => Path.GetFileNameWithoutExtension(FilePath);

        public string FileName => Path.GetFileName(FilePath);

        public bool IsVideo { get; set; }

        // Control de expansión
        [ObservableProperty]
        private bool _isExpanded;

        // Información del archivo
        public string Format => Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();

        public string FileSize
        {
            get
            {
                try
                {
                    long bytes = new FileInfo(FilePath).Length;

                    string[] units = { "B", "KB", "MB", "GB" };

                    double size = bytes;
                    int unit = 0;

                    while (size >= 1024 && unit < units.Length - 1)
                    {
                        size /= 1024;
                        unit++;
                    }

                    return $"{size:F2} {units[unit]}";
                }
                catch
                {
                    return "Desconocido";
                }
            }
        }

        public string CreationDate
        {
            get
            {
                try
                {
                    return File.GetCreationTime(FilePath).ToString("dd/MM/yyyy");
                }
                catch
                {
                    return "Desconocida";
                }
            }
        }

        public string ModificationDate
        {
            get
            {
                try
                {
                    return File.GetLastWriteTime(FilePath).ToString("dd/MM/yyyy");
                }
                catch
                {
                    return "Desconocida";
                }
            }
        }

        public string Duration { get; set; } = "No disponible";

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".avi", ".mov", ".webm", ".m4v"
        };

        private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".aac", ".flac", ".ogg",
            ".wav", ".m4a", ".opus"
        };

        public static MediaItem? TryCreate(string path)
        {
            var ext = Path.GetExtension(path);

            if (VideoExtensions.Contains(ext))
            {
                return new MediaItem
                {
                    FilePath = path,
                    IsVideo = true
                };
            }

            if (AudioExtensions.Contains(ext))
            {
                return new MediaItem
                {
                    FilePath = path,
                    IsVideo = false
                };
            }

            return null;
        }
    }
}
