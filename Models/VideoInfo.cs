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
    }
}
