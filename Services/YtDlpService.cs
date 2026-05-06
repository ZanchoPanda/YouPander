using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Web;
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

        private readonly List<Process> _activeProcesses = new();
        private readonly object _processLock = new();

        /// <summary>
        /// Mata el proceso en curso y limpia la referencia.
        /// </summary>
        public void KillCurrentProcess()
        {
            #region Version 1

            //try
            //{
            //    if (_currentProcess != null && !_currentProcess.HasExited)
            //        _currentProcess.Kill(entireProcessTree: true);
            //}
            //catch { /* El proceso ya terminó */ }
            //finally
            //{
            //    currentProcess = null;
            //}

            #endregion

            #region  Version 2

            lock (_processLock)
            {
                foreach (var p in _activeProcesses)
                {
                    try
                    {
                        if (!p.HasExited)
                        {
                            p.Kill(entireProcessTree: true);
                        }
                    }
                    catch { }
                }
                _activeProcesses.Clear();
            }

            #endregion

        }

        public async Task DownloadAsync(string url, string output, string format, IProgress<string> progress, CancellationToken token, string? formatID = null)
        {

            string[] parts = BuildArguments(url, output, format, formatID);
            string args = string.Join(" ", parts);

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

            //process.Start();
            lock (_processLock) _activeProcesses.Add(process);

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
                lock (_processLock) _activeProcesses.Remove(process);
                currentProcess = null;
            }
        }
        
        public async Task DownloadAsync(string url, string output, string format, string? formatID = null)
        {
            string[] parts = BuildArguments(url, output, format, formatID);
            string args = string.Join(" ", parts);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _path,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // Drenar stdout y stderr para evitar deadlock por buffer lleno
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync());
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

        public async Task<List<FormatOption>> FetchFormatsAsync(string url, CancellationToken ct = default)
        {
            #region Version 3

            string args = IsPlaylist(url)
                ? $"-J --playlist-items 1 \"{url}\""
                : $"-J \"{url}\"";

            var raw = await Task.Run(() => RunAndCaptureAsync(args, ct), ct);

            int jsonIndex = FindJsonStart(raw);
            if (jsonIndex < 0)
            {
                return new List<FormatOption>();
            }

            using var doc = JsonDocument.Parse(raw.AsMemory(jsonIndex));
            var root = doc.RootElement;

            JsonElement target = root;
            if (root.TryGetProperty("entries", out var entries))
            {
                var first = entries.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Undefined)
                    return new List<FormatOption>();
                target = first;
            }

            if (!target.TryGetProperty("formats", out var formats))
                return new List<FormatOption>();

            return ParseBestFormats(formats);

            #endregion
        }

        private static bool IsPlaylist(string url) => 
            url.Contains("list=", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("/playlist", StringComparison.OrdinalIgnoreCase);

        private static int FindJsonStart(string raw)
        {
            int i = 0;
            while (i < raw.Length)
            {
                int nl = raw.IndexOf('\n', i);
                int end = nl < 0 ? raw.Length : nl;
                var slice = raw.AsSpan(i, end - i).TrimStart();
                if (slice.StartsWith("{") || slice.StartsWith("["))
                    return i + (end - i - slice.Length); 
                i = end + 1;
            }
            return -1;
        }

        private static List<FormatOption> ParseBestFormats(JsonElement formats)
        {
            FormatOption? bestVideo = null;
            FormatOption? bestAudio = null;

            bestAudio = new FormatOption
            {
                FormatId = "mp3",
                Extension = "mp3",
                IsVideo = false,
                Abr = 320,
                Label = "Audio MP3 — ~320kbps"
            };

            foreach (var f in formats.EnumerateArray())
            {
                string vcodec = f.GetStringOrEmpty("vcodec") ?? "none";
                string acodec = f.GetStringOrEmpty("acodec") ?? "none";
                string ext = f.GetStringOrEmpty("ext") ?? "";
                string fmtId = f.GetStringOrEmpty("format_id") ?? "";

                if ((vcodec == "none" && acodec == "none")
                    || ext is "mhtml" or "3gp" or "flv")
                    continue;

                bool hasVideo = vcodec != "none";
                bool hasAudio = acodec != "none";

                var height = f.TryGetProperty("height", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetInt32() : 0;
                var tbr = f.TryGetProperty("tbr", out var tb) && tb.ValueKind == JsonValueKind.Number ? tb.GetDouble() : 0;
                var abr = f.TryGetProperty("abr", out var ab) && ab.ValueKind == JsonValueKind.Number ? ab.GetDouble() : 0;
                var fps = f.TryGetProperty("fps", out var fp) && fp.ValueKind == JsonValueKind.Number ? fp.GetInt32() : 0;

                if (hasVideo && height > 0)
                {
                    var candidate = new FormatOption
                    {
                        FormatId = fmtId,
                        Extension = ext,
                        ResolutionInt = height,
                        Fps = fps,
                        Tbr = tbr,
                        IsVideo = true,
                        Label = $"Video {ext.ToUpper()} — {height}p{(fps >= 60 ? $" {fps}fps" : "")}"
                    };

                    if (bestVideo is null
                        || height > bestVideo.ResolutionInt
                        || (height == bestVideo.ResolutionInt && tbr > bestVideo.Tbr))
                    {
                        bestVideo = candidate;
                    }
                }
            }

            var result = new List<FormatOption>();
            if (bestVideo != null) result.Add(bestVideo);
            result.Add(bestAudio); 
            return result;
        }

        #region Clean URL (no list/playlists)

        public string CleanYouTubeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return url;

            var query = HttpUtility.ParseQueryString(uri.Query);

            // Parámetros que queremos eliminar
            string[] keysToRemove = { "list", "index", "start_radio", "pp" };

            foreach (var key in keysToRemove)
            {
                query.Remove(key);
            }

            // Reconstruir query limpia
            var newQuery = string.Join("&",
                query.AllKeys
                     .Where(k => !string.IsNullOrEmpty(k))
                     .Select(k => $"{k}={query[k]}"));

            var uriBuilder = new UriBuilder(uri)
            {
                Query = newQuery
            };

            return uriBuilder.Uri.ToString();
        }

        #endregion

        #region Get Info from URL

        public async Task<List<VideoInfo>> FetchInfoAsync(string url, CancellationToken ct = default)
        {
            var raw = await RunAndCaptureAsync($"--flat-playlist -J \"{url}\"", ct);

            var jsonStart = raw.Split('\n').FirstOrDefault(line => line.TrimStart().StartsWith("{") || line.TrimStart().StartsWith("["));

            if (jsonStart == null)
                throw new Exception("yt-dlp no devolvió JSON válido.");

            var jsonOnly = raw.Substring(raw.IndexOf(jsonStart, StringComparison.Ordinal));

            using var doc = JsonDocument.Parse(jsonOnly);
            var root = doc.RootElement;

            var results = new List<VideoInfo>();

            if (root.TryGetProperty("entries", out var entries))
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    var info = ParseEntry(entry);

                    if (string.IsNullOrWhiteSpace(info.Url)) continue;
                    if (info.Title == "[Private video]" || info.Title == "[Deleted video]") continue;

                    results.Add(info);
                }
            }
            else
            {
                results.Add(ParseEntry(root));
            }

            return results;
        }

        private static VideoInfo ParseEntry(JsonElement el)
        {
            try
            {

                string Duration = el.TryGetProperty("duration", out var d)
                    && d.ValueKind == JsonValueKind.Number
                    && d.TryGetDouble(out double seconds)
                    ? TimeSpan.FromSeconds(seconds).ToString(@"m\:ss")
                    : string.Empty;

                string thumbnail = string.Empty;

                if (el.TryGetProperty("thumbnails", out var thumbs) && thumbs.ValueKind == JsonValueKind.Array)
                {
                    thumbnail = thumbs.EnumerateArray()
                        .Where(t => t.TryGetProperty("url", out _))
                        .OrderByDescending(t => t.TryGetProperty("width", out var w) && w.ValueKind == JsonValueKind.Number
                        ? w.GetInt32() : 0)
                        .Select(t => t.GetProperty("url").GetString())
                        .FirstOrDefault() ?? string.Empty;
                }

                var id = el.GetStringOrEmpty("id");

                var url = !string.IsNullOrWhiteSpace(el.GetStringOrEmpty("webpage_url")) ? el.GetStringOrEmpty("webpage_url") :
                    !string.IsNullOrWhiteSpace(el.GetStringOrEmpty("url")) ? el.GetStringOrEmpty("url") :
                    !string.IsNullOrWhiteSpace(el.GetStringOrEmpty("original_url")) ? el.GetStringOrEmpty("original_url") :
                    BuildUrlFromId(el, id);

                return new VideoInfo
                {
                    Id = id,
                    Title = el.GetStringOrEmpty("title"),
                    Channel = el.GetStringOrEmpty("channel") ?? el.GetStringOrEmpty("uploader"),
                    Thumbnail = thumbnail,
                    Url = url,
                    Duration = Duration
                };
            }
            catch (Exception ex)
            {
                var aux = ex;
                throw;
            }
        }

        private static string BuildUrlFromId(JsonElement el, string id)
        {
            if (string.IsNullOrEmpty(id))
                return string.Empty;

            var extractor = el.GetStringOrEmpty("ie_key")
                         ?? el.GetStringOrEmpty("extractor")
                         ?? string.Empty;

            return extractor?.ToLowerInvariant() switch
            {
                "youtube" or "youtubetab" => $"https://www.youtube.com/watch?v={id}",
                "soundcloud" => string.Empty, 
                "twitch:vod" => $"https://www.twitch.tv/videos/{id}",
                _ => $"https://www.youtube.com/watch?v={id}" 
            };
        }

        public async Task<string> RunAndCaptureAsync(string arguments, CancellationToken ct = default)
        {
            var sb = new StringBuilder();

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _path,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    sb.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    sb.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
                throw new Exception($"yt-dlp error (code {process.ExitCode}):\n{sb}");

            return sb.ToString();
        }

        #endregion

        #region Args Constructor

        //private string[] BuildArguments(string url, string output, string format)
        //{
        //    string[] commonArgs =
        //    [
        //        $"-o \"{output}/%(title)s.%(ext)s\"",
        //        $"--ffmpeg-location \"{_ffmpegPath}\"",
        //        "--newline"
        //    ];

        //    string[] formatArgs;

        //    if (format.Contains("Audio"))
        //    {
        //        formatArgs =
        //        [
        //            "-x",
        //            "--audio-format mp3",
        //            "--audio-quality 0",
        //            "--embed-metadata",
        //            "--embed-thumbnail"
        //        ];
        //    }
        //    else
        //    {
        //        formatArgs =
        //        [
        //            "-f \"bestvideo+bestaudio/best\"",
        //            "--merge-output-format mkv",
        //        ];
        //    }

        //    //Return the combined arguments with the URL at the end
        //    return [.. commonArgs, .. formatArgs, url];
        //}

        private string[] BuildArguments(string url, string output, string format, string? formatId = null)
        {
            string[] commonArgs =
            [
                $"-o \"{output}/%(title)s.%(ext)s\"",
                $"--ffmpeg-location \"{_ffmpegPath}\"",
                "--newline",
                "--embed-metadata",
                "--embed-thumbnail"
            ];

            string[] formatArgs;

            if (formatId == "mp3")
            {
                formatArgs =
                [
                    "-x",
                    "--audio-format mp3",
                    "--audio-quality 0",
                ];
            }
            else if (!string.IsNullOrEmpty(formatId))
            {
                formatArgs = [$"-f \"{formatId}\""];
            }
            else if (format.Contains("Audio"))
            {
                formatArgs =
                [
                    "-x",
                    "--audio-format mp3",
                    "--audio-quality 0",
                ];
            }
            else
            {
                formatArgs =
                [
                    "-f \"bestvideo+bestaudio/best\"",
                    "--merge-output-format mkv",
                ];
            }

            return [.. commonArgs, .. formatArgs, url];
        }

        #endregion

    }
}

