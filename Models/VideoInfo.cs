using System;
using System.Collections.Generic;
using System.Text;

namespace YouPander.Models
{
    public class VideoInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string Thumbnail { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;

        // Metadata musical proporcionada por yt-dlp
        public string? Artist { get; set; }
        public List<string>? Artists { get; set; }
        public string? Album { get; set; }
        public string? AlbumArtist { get; set; }
        public int? TrackNumber { get; set; }
        public int? DiscNumber { get; set; }
        public string? Genre { get; set; }
        public string? ReleaseDate { get; set; }

        public string? Description { get; set; }
        public double? DurationSeconds { get; set; }


    }
}
