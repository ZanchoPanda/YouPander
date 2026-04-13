using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Runtime.Versioning;
using YouPander.Resources.Localization;
using YouPander.Models;
using YouPander.ViewModels;

namespace YouPander.Services
{
    [SupportedOSPlatform("windows")]
    public class YtDlpService : BaseViewModel
    {
        private readonly string _path;

        private Process? _currentProcess;
        public Process? currentProcess
        {
            get
            {
                return _currentProcess;
            }
            set
            {
                if (value != _currentProcess)
                {
                    _currentProcess = value;
                    OnPropertyChanged("currentProcess");
                }
            }
        }


        public YtDlpService(string path)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("YtDlpService solo es compatible en Windows.");

            _path = path;
        }

        public async Task EnsureInstalledAsync()
        {
            if (File.Exists(_path))
                return;

            string url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

            string? dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using HttpClient client = new HttpClient();
            using HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using Stream stream = await response.Content.ReadAsStreamAsync();
            using var fs = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fs);
        }

        public async Task DownloadAsync(string url, string output, string format, IProgress<string> progress, CancellationToken token)
        {
            string args = $"-o \"{output}/%(title)s.%(ext)s\" ";

            if (format.Contains("Audio"))
            {
                args += $"-x --audio-format mp3 --audio-quality 0 {url}";
            }
            else
            {
                args = $"-o \"{output}/%(title)s.%(ext)s\" {url} --newline";
            }

            using Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _path,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            currentProcess = process;

            process.Start();

            string? Finalline;
            while ((Finalline = await process.StandardOutput.ReadLineAsync()) != null)
            {
                if (currentProcess == null)
                {
                    return;
                }

                var parsed = ParseProgress(Finalline);

                if (!string.IsNullOrEmpty(parsed))
                {
                    progress?.Report(parsed);
                }
                await Task.Delay(50);
            }

            await process.WaitForExitAsync();
           
        }

        public string ParseProgress(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return string.Empty;

            // Descarga en progreso
            if (line.Contains("[download]"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    line,
                    @"(\d{1,3}\.\d+)%.*?at\s+([\d\.]+\w+/s).*?ETA\s+([\d:]+)"
                );

                if (match.Success)
                {
                    var percent = match.Groups[1].Value;
                    var speed = match.Groups[2].Value;
                    var eta = match.Groups[3].Value;

                    return $"{Strings.Downloading}... {percent}% ( {speed} )";
                }

                return $"{Strings.Downloading}...";
            }

            // Extrayendo info
            if (line.Contains("[info]"))
                return $"{Strings.GettingInfo}...";

            // Convirtiendo a MP3
            if (line.Contains("Extracting audio"))
                return $"{Strings.ExtractingAudio}...";

            // Merge de vídeo/audio
            if (line.Contains("Merging formats"))
                return $"{Strings.MergingAudioVideo}...";

            // Finalizado
            if (line.Contains("100%"))
                return $"{Strings.CompletedDownload}";

            return string.Empty;
        }

    }
}
