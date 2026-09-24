using System;
using System.Collections.Generic;
using System.Text;

namespace YouPander.Models
{
    public class SongMetadata
    {
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Album { get; set; }
        public string? AlbumArtist { get; set; }

        public int? TrackNumber { get; set; }
        public int? DiscNumber { get; set; }

        public int? Year { get; set; }
        public string? Genre { get; set; }

        public string? CoverUrl { get; set; }

        public string? Isrc { get; set; }

        public string? RecordingMbid { get; set; }
        public string? ReleaseMbid { get; set; }
    }

    public class SongCandidate
    {
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public double? DurationSeconds { get; set; }

        public string? Album { get; set; }
        public string? AlbumArtist { get; set; }
        public int? TrackNumber { get; set; }
        public int? DiscNumber { get; set; }
        public int? Year { get; set; }
        public string? Genre { get; set; }
        public string? CoverUrl { get; set; }
    }

}
