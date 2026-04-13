using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Runtime.Versioning;
using System.Text;
using YouPander.Models;
using YouPander.Resources.Localization;
using YouPander.ViewModels;

namespace YouPander.Services
{
    [SupportedOSPlatform("windows")]
    public class YtDlpService : BaseViewModel
    {
        #region Params

        private readonly string _path;
        private readonly string _ffmpegPath;

        private Process? _currentProcess;
        public Process? currentProcess
        {
            get => _currentProcess;
            private set // <- private: solo se asigna internamente
            {
                if (value != _currentProcess)
                {
                    _currentProcess = value;
                    OnPropertyChanged(nameof(currentProcess));
                }
            }
        }

        #endregion

        public YtDlpService(string path)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("YtDlpService solo es compatible en Windows.");

            _path = path;
            _ffmpegPath = Path.Combine(Path.GetDirectoryName(path)!, "ffmpeg.exe");
        }

        #region yt-dlp

        /// <summary>
        /// Descarga yt-dlp.exe si no existe todavía.
        /// </summary>
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

        #endregion

        #region Ffmpeg

        public async Task EnsureFfmpegInstalledAsync()
        {
            if (File.Exists(_ffmpegPath)) return;

            // FFmpeg build estático para Windows (from github.com/BtbN/FFmpeg-Builds)
            string zipUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
            string zipPath = Path.Combine(Path.GetDirectoryName(_ffmpegPath)!, "ffmpeg.zip");

            await DownloadFileAsync(zipUrl, zipPath);
            await ExtractFfmpegAsync(zipPath);
        }

        private async Task ExtractFfmpegAsync(string zipPath)
        {
            string outputDir = Path.GetDirectoryName(_ffmpegPath)!;

            await Task.Run(() =>
            {
                using var zip = System.IO.Compression.ZipFile.OpenRead(zipPath);

                // El zip contiene una carpeta raíz, buscamos ffmpeg.exe dentro de /bin/
                var entry = zip.Entries.FirstOrDefault(e =>
                    e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase) &&
                    e.FullName.Contains("bin"));

                if (entry == null)
                    throw new FileNotFoundException("No se encontró ffmpeg.exe en el zip.");

                entry.ExtractToFile(_ffmpegPath, overwrite: true);
            });

            File.Delete(zipPath); // Limpiamos el zip
        }

        private async Task DownloadFileAsync(string url, string destinationPath)
        {
            string? dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using HttpClient client = new HttpClient();
            using HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using Stream stream = await response.Content.ReadAsStreamAsync();
            using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fs);
        }
        #endregion

        /// <summary>
        /// Mata el proceso en curso y limpia la referencia.
        /// </summary>
        public void KillCurrentProcess()
        {
            try
            {
                if (_currentProcess != null && !_currentProcess.HasExited)
                    _currentProcess.Kill(entireProcessTree: true);
            }
            catch { /* El proceso ya terminó */ }
            finally
            {
                currentProcess = null;
            }
        }

        public async Task DownloadAsync(string url, string output, string format, IProgress<string> progress, CancellationToken token)
        {

            string args;
            string[] parts;

            if (format.Contains("Audio"))
            {
                parts = new[]
                {
                    $"-o \"{output}/%(title)s.%(ext)s\"",
                    $"--ffmpeg-location \"{_ffmpegPath}\"",
                    "-x --audio-format mp3 --audio-quality 0",
                    "--embed-metadata --embed-thumbnail",
                    "--newline"
                };
            }
            else
            {
                parts = new[]
                {
                    $"-o \"{output}/%(title)s.%(ext)s\"",
                    $"--newline",
                };
            }

            parts = parts.Append(url).ToArray();
            args = string.Join(" ", parts);

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

            try
            {
                process.Start();

                // Leer stderr en paralelo para evitar que el buffer se llene y bloquee el proceso
                var stderrTask = ConsumeStreamAsync(process.StandardError, progress, token);

                string? line;
                while ((line = await process.StandardOutput.ReadLineAsync(token)) != null)
                {
                    token.ThrowIfCancellationRequested();

                    var parsed = ParseProgress(line);
                    if (!string.IsNullOrEmpty(parsed))
                        progress?.Report(parsed);
                }

                await Task.WhenAll(
                    process.WaitForExitAsync(token),
                    stderrTask
                );
            }
            finally
            {
                currentProcess = null;
            }
        }

        // Consume el stream sin bloquearlo, opcionalmente loguea errores
        private async Task ConsumeStreamAsync(StreamReader reader, IProgress<string>? progress, CancellationToken token)
        {
            try
            {
                string? line;
                while ((line = await reader.ReadLineAsync(token)) != null)
                {
                    token.ThrowIfCancellationRequested();

                    // Reportar errores reales de yt-dlp
                    if (line.Contains("ERROR") || line.Contains("error"))
                        progress?.Report($"⚠️ {line}");
                }
            }
            catch (OperationCanceledException) { }
        }

        public string ParseProgress(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return string.Empty;

            if (line.Contains("[download]"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    line,
                    @"(\d{1,3}[\.,]\d+)%.*?at\s+([\d\.]+\w+/s).*?ETA\s+([\d:]+)"
                );

                if (match.Success)
                {
                    var percent = match.Groups[1].Value;
                    var speed = match.Groups[2].Value;
                    return $"{Strings.Downloading}... {percent}% ( {speed} )";
                }

                return $"{Strings.Downloading}...";
            }

            if (line.Contains("[info]") || line.Contains("[youtube]"))
            {
                // Solo mostrar info relevante, no cada metadata intermedia
                if (line.Contains("Downloading") || line.Contains("Extracting URL"))
                    return $"{Strings.GettingInfo}...";

                if (line.Contains("Extracting audio"))
                    return $"{Strings.ExtractingAudio}...";

                if (line.Contains("Merging formats"))
                    return $"{Strings.MergingAudioVideo}...";

                if (line.Contains("[PostProcess]") || line.Contains("post-process"))
                    return $"{Strings.ExtractingAudio}...";

                if (line.Contains("100%"))
                    return $"{Strings.CompletedDownload}";

                return string.Empty; // Ignorar el resto de líneas [info]
            }

            return string.Empty;
        }
    }
}