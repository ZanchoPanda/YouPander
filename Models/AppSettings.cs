using System;
using System.Collections.Generic;
using System.Text;
using YouPander.ViewModels;

namespace YouPander.Models
{
    public class AppSettings : BaseViewModel
    {

		private string _DownloadPath;
		public string DownloadPath
		{
			get
			{
				return _DownloadPath;
			}
			set
			{
				if (value != _DownloadPath)
				{
					_DownloadPath = value;
					OnPropertyChanged("DownloadPath");
				}
			}
		}

		private string _Language;
		public string Language
		{
			get
			{
				return _Language;
			}
			set
			{
				if (value != _Language)
				{
					_Language = value;
					OnPropertyChanged("Language");
				}
			}
		}

		private string _ThemeColor;
		public string ThemeColor
		{
			get
			{
				return _ThemeColor;
			}
			set
			{
				if (value != _ThemeColor)
				{
					_ThemeColor = value;
					OnPropertyChanged("ThemeColor");
				}
			}
		}

		private bool _DarkMode;
		public bool DarkMode
		{
			get
			{
				return _DarkMode;
			}
			set
			{
				if (value != _DarkMode)
				{
					_DarkMode = value;
					OnPropertyChanged("DarkMode");
				}
			}
		}

		private bool _OpenDownloads;
		public bool OpenDownloads
		{
			get
			{
				return _OpenDownloads;
			}
			set
			{
				if (value != _OpenDownloads)
				{
					_OpenDownloads = value;
					OnPropertyChanged("OpenDownloads");
				}
			}
		}


		public AppSettings()
        {
            DownloadPath = FileSystem.AppDataDirectory;
            Language = "en";
            ThemeColor = "#2196F3";
            DarkMode = true;
        }

    }
}
