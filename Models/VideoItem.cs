using System;
using System.Collections.Generic;
using System.Text;
using YouPander.ViewModels;

namespace YouPander.Models
{
    public class VideoItem : BaseViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;

		private bool _IsSelected;
		public bool IsSelected
		{
			get
			{
				return _IsSelected;
			}
			set
			{
				if (value != _IsSelected)
				{
					_IsSelected = value;
					OnPropertyChanged("IsSelected");
				}
			}
		}

		private double _Progress;
		public double Progress
		{
			get
			{
				return _Progress;
			}
			set
			{
				if (value != _Progress)
				{
					_Progress = value;
					OnPropertyChanged("Progress");
				}
			}
		}

		private string _Status = string.Empty;
		public string Status
		{
			get
			{
				return _Status;
			}
			set
			{
				if (value != _Status)
				{
					_Status = value;
					OnPropertyChanged("Status");
				}
			}
		}

		private bool _IsDownloaded;
		public bool IsDownloaded
		{
			get
			{
				return _IsDownloaded;
			}
			set
			{
				if (value != _IsDownloaded)
				{
					_IsDownloaded = value;
					OnPropertyChanged("IsDownloaded");
				}
			}
		}


	}
}
